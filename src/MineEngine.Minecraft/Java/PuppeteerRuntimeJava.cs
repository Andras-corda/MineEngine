namespace MineEngine.Minecraft.Java;

/// <summary>
/// Java engine of the Puppeteer behavior trees, copied into the mod when it uses some:
/// statuses, blackboard, nodes (composites, tasks), decorators, services, and the Goal that
/// runs the tree on every tick. Written for Minecraft 1.20.1 to 1.21.1; only the creation
/// of projectiles and ResourceLocations depends on the version.
/// </summary>
public static class PuppeteerRuntimeJava
{
    public const string ClassName = "Puppeteer";

    /// <summary>Full source of the Puppeteer class of the package <paramref name="package"/>.</summary>
    public static string Source(string package, JavaGameApi api, string notice)
    {
        ArgumentNullException.ThrowIfNull(api);
        (string arrow, string fireball) = api.ProjectileCreation;
        string location = api.ParseResourceLocation("id");
        return $$"""
package {{package}};

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.EnumSet;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Random;
import net.minecraft.core.registries.BuiltInRegistries;
import net.minecraft.resources.ResourceLocation;
import net.minecraft.sounds.SoundEvent;
import net.minecraft.world.InteractionHand;
import net.minecraft.world.entity.Entity;
import net.minecraft.world.entity.LivingEntity;
import net.minecraft.world.entity.PathfinderMob;
import net.minecraft.world.entity.ai.goal.Goal;
import net.minecraft.world.entity.ai.navigation.PathNavigation;
import net.minecraft.world.entity.ai.util.DefaultRandomPos;
import net.minecraft.world.entity.ai.util.LandRandomPos;
import net.minecraft.world.entity.player.Player;
import net.minecraft.world.entity.projectile.Arrow;
import net.minecraft.world.entity.projectile.Projectile;
import net.minecraft.world.entity.projectile.SmallFireball;
import net.minecraft.world.entity.projectile.Snowball;
import net.minecraft.world.item.ItemStack;
import net.minecraft.world.item.Items;
import net.minecraft.world.level.Level;
import net.minecraft.world.phys.Vec3;

/**
 * Engine of the Puppeteer behavior trees: every mob builds its own tree,
 * run on every tick by {@link TreeGoal}.
{{notice}}
 */
public final class Puppeteer {
    private Puppeteer() {
    }

    /** Result of a node on a given tick. */
    public enum Status { SUCCESS, FAILURE, RUNNING }

    /** Abort modes of a condition (Unreal's "Observer aborts"). */
    public static final int ABORT_NONE = 0;
    public static final int ABORT_SELF = 1;
    public static final int ABORT_LOWER = 2;
    public static final int ABORT_BOTH = 3;

    /** Comparisons of the conditions. */
    public static final int LESS = 0;
    public static final int LESS_OR_EQUAL = 1;
    public static final int EQUAL = 2;
    public static final int GREATER_OR_EQUAL = 3;
    public static final int GREATER = 4;

    static boolean compare(double left, int operator, double right) {
        switch (operator) {
            case LESS: return left < right;
            case LESS_OR_EQUAL: return left <= right;
            case EQUAL: return Math.abs(left - right) < 1.0E-6;
            case GREATER_OR_EQUAL: return left >= right;
            default: return left > right;
        }
    }

    static int ticks(double seconds) {
        return (int) Math.max(0, Math.round(seconds * 20.0));
    }

    static ResourceLocation location(String id) {
        return {{location}};
    }

    // ----- Blackboard -----

    /** Memory of the mob: named values shared by the nodes of its tree. */
    public static final class Blackboard {
        private final Map<String, Object> values = new HashMap<>();

        public void set(String key, Object value) {
            if (value == null) {
                values.remove(key);
            } else {
                values.put(key, value);
            }
        }

        public void clear(String key) {
            values.remove(key);
        }

        /** True when the key has a value; a dead or removed creature clears the key. */
        public boolean has(String key) {
            Object value = values.get(key);
            if (value instanceof Entity entity && (!entity.isAlive() || entity.isRemoved())) {
                values.remove(key);
                return false;
            }
            return value != null;
        }

        public LivingEntity entity(String key) {
            return has(key) && values.get(key) instanceof LivingEntity living ? living : null;
        }

        /** Position of a key: the position itself, or that of the creature. */
        public Vec3 position(String key) {
            if (!has(key)) {
                return null;
            }
            Object value = values.get(key);
            if (value instanceof Entity entity) {
                return entity.position();
            }
            return value instanceof Vec3 position ? position : null;
        }

        public double number(String key) {
            return values.get(key) instanceof Number number ? number.doubleValue() : 0.0;
        }

        public boolean bool(String key) {
            return values.get(key) instanceof Boolean flag && flag;
        }
    }

    /** What a node sees when it runs: the mob, its memory and the current tick. */
    public static final class Context {
        public final PathfinderMob mob;
        public final Blackboard blackboard = new Blackboard();
        public long tick;

        Context(PathfinderMob mob) {
            this.mob = mob;
        }

        public PathNavigation navigation() {
            return mob.getNavigation();
        }
    }

    // ----- Nodes -----

    /** A node of the tree, with its decorators (checked in order) and its services. */
    public abstract static class Node {
        final List<Decorator> decorators = new ArrayList<>();
        final List<Service> services = new ArrayList<>();
        long startTick;
        private boolean running;

        public Node decorate(Decorator decorator) {
            decorators.add(decorator);
            return this;
        }

        public Node service(Service service) {
            services.add(service);
            return this;
        }

        public final Status tick(Context context) {
            if (!running) {
                for (Decorator decorator : decorators) {
                    if (!decorator.allows(context, false)) {
                        return Status.FAILURE;
                    }
                }
                running = true;
                startTick = context.tick;
                for (Decorator decorator : decorators) {
                    decorator.onStart(context);
                }
                for (Service service : services) {
                    service.onStart(context);
                }
                start(context);
            } else {
                for (Decorator decorator : decorators) {
                    if (decorator.observesSelf() && !decorator.allows(context, true)) {
                        abort(context);
                        return Status.FAILURE;
                    }
                }
            }
            for (Service service : services) {
                service.tick(context);
            }
            Status status = update(context);
            for (Decorator decorator : decorators) {
                status = decorator.filter(context, this, status);
            }
            if (status != Status.RUNNING) {
                end(context);
            }
            return status;
        }

        /** True when a "lower priority" condition of this node became true: it takes over. */
        final boolean wantsToInterrupt(Context context) {
            boolean observed = false;
            for (Decorator decorator : decorators) {
                observed |= decorator.observesLower();
                if (!decorator.allows(context, false)) {
                    return false;
                }
            }
            return observed;
        }

        /** Stops the node when it is running. */
        public final void abort(Context context) {
            if (running) {
                end(context);
            }
        }

        /** Restarts the node without checking its conditions again (loop). */
        final void restart(Context context) {
            stop(context);
            startTick = context.tick;
            start(context);
        }

        private void end(Context context) {
            running = false;
            stop(context);
            for (Decorator decorator : decorators) {
                decorator.onFinish(context);
            }
        }

        protected void start(Context context) {
        }

        protected abstract Status update(Context context);

        /** Called when the node ends or is interrupted. */
        protected void stop(Context context) {
        }
    }

    // ----- Composites -----

    public abstract static class Composite extends Node {
        final List<Node> children = new ArrayList<>();
        List<Node> order = children;
        int current;

        public Composite add(Node... nodes) {
            children.addAll(Arrays.asList(nodes));
            return this;
        }

        @Override
        protected void start(Context context) {
            current = 0;
        }

        @Override
        protected void stop(Context context) {
            for (Node child : children) {
                child.abort(context);
            }
        }
    }

    /** Tries its children in order until one succeeds. */
    public static final class Selector extends Composite {
        private final boolean random;

        public Selector(boolean random) {
            this.random = random;
        }

        @Override
        protected void start(Context context) {
            super.start(context);
            if (random) {
                order = new ArrayList<>(children);
                Collections.shuffle(order, new Random(context.mob.getRandom().nextLong()));
            }
        }

        @Override
        protected Status update(Context context) {
            for (int i = 0; i < current && current < order.size(); i++) {
                if (order.get(i).wantsToInterrupt(context)) {
                    order.get(current).abort(context);
                    current = i;
                    break;
                }
            }
            while (current < order.size()) {
                Status status = order.get(current).tick(context);
                if (status != Status.FAILURE) {
                    return status;
                }
                current++;
            }
            return Status.FAILURE;
        }
    }

    /** Runs its children in order until one fails. */
    public static final class Sequence extends Composite {
        @Override
        protected Status update(Context context) {
            while (current < children.size()) {
                Status status = children.get(current).tick(context);
                if (status != Status.SUCCESS) {
                    return status;
                }
                current++;
            }
            return Status.SUCCESS;
        }
    }

    /** The first child is the main task; the others run in the background while it lasts. */
    public static final class SimpleParallel extends Composite {
        @Override
        protected Status update(Context context) {
            if (children.isEmpty()) {
                return Status.SUCCESS;
            }
            Status main = children.get(0).tick(context);
            if (main != Status.RUNNING) {
                return main;
            }
            for (int i = 1; i < children.size(); i++) {
                children.get(i).tick(context);
            }
            return Status.RUNNING;
        }
    }

    // ----- Tasks -----

    public static final class Wait extends Node {
        private final double seconds;
        private final double deviation;
        private long end;

        public Wait(double seconds, double deviation) {
            this.seconds = seconds;
            this.deviation = deviation;
        }

        @Override
        protected void start(Context context) {
            double duration = seconds + (context.mob.getRandom().nextDouble() * 2.0 - 1.0) * deviation;
            end = context.tick + ticks(duration);
        }

        @Override
        protected Status update(Context context) {
            return context.tick >= end ? Status.SUCCESS : Status.RUNNING;
        }
    }

    public static final class MoveTo extends Node {
        private final String target;
        private final double speed;
        private final double acceptance;
        private int repath;

        public MoveTo(String target, double speed, double acceptance) {
            this.target = target;
            this.speed = speed;
            this.acceptance = acceptance;
        }

        @Override
        protected void start(Context context) {
            repath = 0;
        }

        @Override
        protected Status update(Context context) {
            Vec3 destination = context.blackboard.position(target);
            if (destination == null) {
                return Status.FAILURE;
            }
            double distance = context.mob.position().distanceToSqr(destination);
            if (distance <= acceptance * acceptance) {
                return Status.SUCCESS;
            }
            LivingEntity entity = context.blackboard.entity(target);
            if (--repath <= 0) {
                repath = 10;
                boolean moving = entity != null
                    ? context.navigation().moveTo(entity, speed)
                    : context.navigation().moveTo(destination.x, destination.y, destination.z, speed);
                if (!moving) {
                    return Status.FAILURE;
                }
            } else if (entity == null && context.navigation().isDone()) {
                return distance <= (acceptance + 1.0) * (acceptance + 1.0) ? Status.SUCCESS : Status.FAILURE;
            }
            return Status.RUNNING;
        }

        @Override
        protected void stop(Context context) {
            context.navigation().stop();
        }
    }

    public static final class Wander extends Node {
        private final int radius;
        private final double speed;
        private boolean moving;

        public Wander(int radius, double speed) {
            this.radius = radius;
            this.speed = speed;
        }

        @Override
        protected void start(Context context) {
            Vec3 position = LandRandomPos.getPos(context.mob, radius, 7);
            moving = position != null && context.navigation().moveTo(position.x, position.y, position.z, speed);
        }

        @Override
        protected Status update(Context context) {
            if (!moving) {
                return Status.FAILURE;
            }
            return context.navigation().isDone() ? Status.SUCCESS : Status.RUNNING;
        }

        @Override
        protected void stop(Context context) {
            context.navigation().stop();
        }
    }

    public static final class MeleeAttack extends Node {
        private final String target;
        private final double speed;
        private final int cooldown;
        private long readyAt;
        private int repath;

        public MeleeAttack(String target, double speed, double cooldownSeconds) {
            this.target = target;
            this.speed = speed;
            this.cooldown = ticks(cooldownSeconds);
        }

        @Override
        protected void start(Context context) {
            repath = 0;
            context.mob.setAggressive(true);
        }

        @Override
        protected Status update(Context context) {
            LivingEntity victim = context.blackboard.entity(target);
            if (victim == null) {
                return Status.FAILURE;
            }
            context.mob.getLookControl().setLookAt(victim, 30.0F, 30.0F);
            double reach = context.mob.getBbWidth() * 2.0;
            if (context.mob.distanceToSqr(victim) <= reach * reach + victim.getBbWidth()) {
                context.navigation().stop();
                if (context.tick >= readyAt) {
                    readyAt = context.tick + cooldown;
                    context.mob.swing(InteractionHand.MAIN_HAND);
                    context.mob.doHurtTarget(victim);
                    return Status.SUCCESS;
                }
                return Status.RUNNING;
            }
            if (--repath <= 0) {
                repath = 10;
                if (!context.navigation().moveTo(victim, speed)) {
                    return Status.FAILURE;
                }
            }
            return Status.RUNNING;
        }

        @Override
        protected void stop(Context context) {
            context.navigation().stop();
            context.mob.setAggressive(false);
        }
    }

    public static final class LookAt extends Node {
        private final String target;
        private final double seconds;
        private long end;

        public LookAt(String target, double seconds) {
            this.target = target;
            this.seconds = seconds;
        }

        @Override
        protected void start(Context context) {
            end = context.tick + ticks(seconds);
        }

        @Override
        protected Status update(Context context) {
            LivingEntity entity = context.blackboard.entity(target);
            Vec3 position = context.blackboard.position(target);
            if (position == null) {
                return Status.FAILURE;
            }
            if (entity != null) {
                context.mob.getLookControl().setLookAt(entity, 30.0F, 30.0F);
            } else {
                context.mob.getLookControl().setLookAt(position.x, position.y, position.z);
            }
            return context.tick >= end ? Status.SUCCESS : Status.RUNNING;
        }
    }

    public static final class FleeFrom extends Node {
        private final String target;
        private final double distance;
        private final double speed;
        private int repath;

        public FleeFrom(String target, double distance, double speed) {
            this.target = target;
            this.distance = distance;
            this.speed = speed;
        }

        @Override
        protected void start(Context context) {
            repath = 0;
        }

        @Override
        protected Status update(Context context) {
            Vec3 danger = context.blackboard.position(target);
            if (danger == null || context.mob.position().distanceToSqr(danger) >= distance * distance) {
                return Status.SUCCESS;
            }
            if (--repath <= 0 || context.navigation().isDone()) {
                repath = 20;
                Vec3 away = DefaultRandomPos.getPosAway(context.mob, 16, 7, danger);
                if (away == null || !context.navigation().moveTo(away.x, away.y, away.z, speed)) {
                    return Status.FAILURE;
                }
            }
            return Status.RUNNING;
        }

        @Override
        protected void stop(Context context) {
            context.navigation().stop();
        }
    }

    /** Nearest living creature of the given family (players in creative or spectator mode excluded). */
    static LivingEntity nearest(Context context, Class<? extends LivingEntity> type, double range, boolean mustSee) {
        PathfinderMob mob = context.mob;
        LivingEntity best = null;
        double bestDistance = range * range;
        for (LivingEntity entity : mob.level().getEntitiesOfClass(type, mob.getBoundingBox().inflate(range))) {
            if (entity == mob || !entity.isAlive()
                || entity instanceof Player player && (player.isCreative() || player.isSpectator())
                || mustSee && !mob.getSensing().hasLineOfSight(entity)) {
                continue;
            }
            double distance = mob.distanceToSqr(entity);
            if (distance <= bestDistance) {
                best = entity;
                bestDistance = distance;
            }
        }
        return best;
    }

    public static final class FindNearestEntity extends Node {
        private final Class<? extends LivingEntity> type;
        private final double range;
        private final boolean mustSee;
        private final String result;

        public FindNearestEntity(Class<? extends LivingEntity> type, double range, boolean mustSee, String result) {
            this.type = type;
            this.range = range;
            this.mustSee = mustSee;
            this.result = result;
        }

        @Override
        protected Status update(Context context) {
            LivingEntity found = nearest(context, type, range, mustSee);
            context.blackboard.set(result, found);
            return found != null ? Status.SUCCESS : Status.FAILURE;
        }
    }

    public static final class FindRandomPosition extends Node {
        private final int radius;
        private final String result;

        public FindRandomPosition(int radius, String result) {
            this.radius = radius;
            this.result = result;
        }

        @Override
        protected Status update(Context context) {
            Vec3 position = LandRandomPos.getPos(context.mob, radius, 7);
            if (position == null) {
                return Status.FAILURE;
            }
            context.blackboard.set(result, position);
            return Status.SUCCESS;
        }
    }

    public static final class SetBoolean extends Node {
        private final String key;
        private final boolean value;

        public SetBoolean(String key, boolean value) {
            this.key = key;
            this.value = value;
        }

        @Override
        protected Status update(Context context) {
            context.blackboard.set(key, value);
            return Status.SUCCESS;
        }
    }

    public static final class SetNumber extends Node {
        /** 0 : remplacer, 1 : ajouter, 2 : retirer. */
        private final int operation;
        private final String key;
        private final double value;

        public SetNumber(String key, int operation, double value) {
            this.key = key;
            this.operation = operation;
            this.value = value;
        }

        @Override
        protected Status update(Context context) {
            double current = context.blackboard.number(key);
            context.blackboard.set(key, operation == 1 ? current + value : operation == 2 ? current - value : value);
            return Status.SUCCESS;
        }
    }

    public static final class ClearKey extends Node {
        private final String key;

        public ClearKey(String key) {
            this.key = key;
        }

        @Override
        protected Status update(Context context) {
            context.blackboard.clear(key);
            return Status.SUCCESS;
        }
    }

    public static final class PlaySound extends Node {
        private final String sound;
        private final float volume;
        private final float pitch;

        public PlaySound(String sound, double volume, double pitch) {
            this.sound = sound;
            this.volume = (float) volume;
            this.pitch = (float) pitch;
        }

        @Override
        protected Status update(Context context) {
            SoundEvent event = BuiltInRegistries.SOUND_EVENT.get(location(sound));
            if (event == null) {
                return Status.FAILURE;
            }
            context.mob.playSound(event, volume, pitch);
            return Status.SUCCESS;
        }
    }

    public static final class Leap extends Node {
        private final String target;
        private final double height;

        /** @param target targeted key, or null to jump on the spot */
        public Leap(String target, double height) {
            this.target = target;
            this.height = height;
        }

        @Override
        protected Status update(Context context) {
            PathfinderMob mob = context.mob;
            if (!mob.onGround()) {
                return Status.FAILURE;
            }
            Vec3 destination = target == null ? null : context.blackboard.position(target);
            Vec3 motion = mob.getDeltaMovement();
            if (destination != null) {
                Vec3 direction = new Vec3(destination.x - mob.getX(), 0.0, destination.z - mob.getZ());
                if (direction.lengthSqr() > 1.0E-7) {
                    motion = direction.normalize().scale(0.4).add(motion.scale(0.2));
                }
            }
            mob.setDeltaMovement(motion.x, height, motion.z);
            return Status.SUCCESS;
        }
    }

    public static final class ShootProjectile extends Node {
        /** 0: arrow, 1: snowball, 2: small fireball. */
        private final int projectile;
        private final String target;
        private final float velocity;
        private final float inaccuracy;

        public ShootProjectile(int projectile, String target, double velocity, double inaccuracy) {
            this.projectile = projectile;
            this.target = target;
            this.velocity = (float) velocity;
            this.inaccuracy = (float) inaccuracy;
        }

        @Override
        protected Status update(Context context) {
            LivingEntity victim = context.blackboard.entity(target);
            if (victim == null) {
                return Status.FAILURE;
            }
            PathfinderMob mob = context.mob;
            Level level = mob.level();
            double dx = victim.getX() - mob.getX();
            double dy = victim.getY(0.3333) - mob.getEyeY();
            double dz = victim.getZ() - mob.getZ();
            mob.getLookControl().setLookAt(victim, 30.0F, 30.0F);
            mob.swing(InteractionHand.MAIN_HAND);
            if (projectile == 2) {
                Projectile fireball = {{fireball}};
                fireball.setPos(mob.getX(), mob.getY(0.5) + 0.5, mob.getZ());
                level.addFreshEntity(fireball);
                return Status.SUCCESS;
            }
            Projectile shot = projectile == 1 ? new Snowball(level, mob) : {{arrow}};
            double horizontal = Math.sqrt(dx * dx + dz * dz);
            shot.shoot(dx, victim.getY(0.3333) - shot.getY() + horizontal * 0.2, dz, velocity, inaccuracy);
            level.addFreshEntity(shot);
            return Status.SUCCESS;
        }
    }

    public static final class Heal extends Node {
        private final float amount;

        public Heal(double amount) {
            this.amount = (float) amount;
        }

        @Override
        protected Status update(Context context) {
            context.mob.heal(amount);
            return Status.SUCCESS;
        }
    }

    public static final class Teleport extends Node {
        private final int range;

        public Teleport(int range) {
            this.range = range;
        }

        @Override
        protected Status update(Context context) {
            PathfinderMob mob = context.mob;
            for (int attempt = 0; attempt < 16; attempt++) {
                double x = mob.getX() + (mob.getRandom().nextDouble() - 0.5) * 2.0 * range;
                double y = mob.getY() + mob.getRandom().nextInt(2 * range + 1) - range;
                double z = mob.getZ() + (mob.getRandom().nextDouble() - 0.5) * 2.0 * range;
                if (mob.randomTeleport(x, y, z, true)) {
                    mob.getNavigation().stop();
                    return Status.SUCCESS;
                }
            }
            return Status.FAILURE;
        }
    }

    /** Branch built by a public goal; an empty goal does nothing and succeeds. */
    public static Node goal(Node node) {
        return node != null ? node : new Succeed();
    }

    public static final class Succeed extends Node {
        @Override
        protected Status update(Context context) {
            return Status.SUCCESS;
        }
    }

    // ----- Decorators -----

    /** Attached to a node: condition (which can prevent it from starting or interrupt it) or modifier. */
    public abstract static class Decorator {
        boolean inverse;
        int abort;

        /** Inverted condition and abort mode (ABORT_...). */
        public Decorator observe(boolean inverse, int abort) {
            this.inverse = inverse;
            this.abort = abort;
            return this;
        }

        boolean observesSelf() {
            return abort == ABORT_SELF || abort == ABORT_BOTH;
        }

        boolean observesLower() {
            return abort == ABORT_LOWER || abort == ABORT_BOTH;
        }

        /** The condition, without the inversion. */
        protected boolean check(Context context) {
            return true;
        }

        /** True when the node can start (or continue, when observing is true). */
        boolean allows(Context context, boolean observing) {
            return check(context) != inverse;
        }

        void onStart(Context context) {
        }

        void onFinish(Context context) {
        }

        /** Changes the result of the node (loop, time limit, inversion...). */
        Status filter(Context context, Node node, Status status) {
            return status;
        }
    }

    public static final class IsSet extends Decorator {
        private final String key;

        public IsSet(String key) {
            this.key = key;
        }

        @Override
        protected boolean check(Context context) {
            return context.blackboard.has(key);
        }
    }

    public static final class CheckBoolean extends Decorator {
        private final String key;

        public CheckBoolean(String key) {
            this.key = key;
        }

        @Override
        protected boolean check(Context context) {
            return context.blackboard.bool(key);
        }
    }

    public static final class CompareNumber extends Decorator {
        private final String key;
        private final int operator;
        private final double value;

        public CompareNumber(String key, int operator, double value) {
            this.key = key;
            this.operator = operator;
            this.value = value;
        }

        @Override
        protected boolean check(Context context) {
            return compare(context.blackboard.number(key), operator, value);
        }
    }

    public static final class CheckDistance extends Decorator {
        private final String target;
        private final int operator;
        private final double value;

        public CheckDistance(String target, int operator, double value) {
            this.target = target;
            this.operator = operator;
            this.value = value;
        }

        @Override
        protected boolean check(Context context) {
            Vec3 position = context.blackboard.position(target);
            return position != null && compare(Math.sqrt(context.mob.position().distanceToSqr(position)), operator, value);
        }
    }

    public static final class CheckHealth extends Decorator {
        private final int operator;
        private final double percent;

        public CheckHealth(int operator, double percent) {
            this.operator = operator;
            this.percent = percent;
        }

        @Override
        protected boolean check(Context context) {
            return compare(context.mob.getHealth() * 100.0 / Math.max(1.0F, context.mob.getMaxHealth()), operator, percent);
        }
    }

    public static final class CheckTimeOfDay extends Decorator {
        private final boolean day;

        public CheckTimeOfDay(boolean day) {
            this.day = day;
        }

        @Override
        protected boolean check(Context context) {
            return context.mob.level().isDay() == day;
        }
    }

    public static final class IsInWater extends Decorator {
        @Override
        protected boolean check(Context context) {
            return context.mob.isInWaterOrBubble();
        }
    }

    public static final class CanSee extends Decorator {
        private final String target;

        public CanSee(String target) {
            this.target = target;
        }

        @Override
        protected boolean check(Context context) {
            LivingEntity entity = context.blackboard.entity(target);
            return entity != null && context.mob.getSensing().hasLineOfSight(entity);
        }
    }

    /** Drawn only when the node starts. */
    public static final class RandomChance extends Decorator {
        private final double percent;

        public RandomChance(double percent) {
            this.percent = percent;
        }

        @Override
        boolean allows(Context context, boolean observing) {
            return observing || (context.mob.getRandom().nextDouble() * 100.0 < percent) != inverse;
        }
    }

    public static final class Cooldown extends Decorator {
        private final int delay;
        private long readyAt;

        public Cooldown(double seconds) {
            this.delay = ticks(seconds);
        }

        @Override
        boolean allows(Context context, boolean observing) {
            return observing || context.tick >= readyAt;
        }

        @Override
        void onFinish(Context context) {
            readyAt = context.tick + delay;
        }
    }

    public static final class Loop extends Decorator {
        /** 0: forever. */
        private final int count;
        private int done;

        public Loop(int count) {
            this.count = count;
        }

        @Override
        void onStart(Context context) {
            done = 0;
        }

        @Override
        Status filter(Context context, Node node, Status status) {
            if (status == Status.RUNNING) {
                return status;
            }
            done++;
            if (count == 0 || done < count) {
                node.restart(context);
                return Status.RUNNING;
            }
            return status;
        }
    }

    public static final class TimeLimit extends Decorator {
        private final int limit;

        public TimeLimit(double seconds) {
            this.limit = Math.max(1, ticks(seconds));
        }

        @Override
        Status filter(Context context, Node node, Status status) {
            return status == Status.RUNNING && context.tick - node.startTick >= limit ? Status.FAILURE : status;
        }
    }

    public static final class ForceSuccess extends Decorator {
        @Override
        Status filter(Context context, Node node, Status status) {
            return status == Status.FAILURE ? Status.SUCCESS : status;
        }
    }

    public static final class InvertResult extends Decorator {
        @Override
        Status filter(Context context, Node node, Status status) {
            return status == Status.SUCCESS ? Status.FAILURE : status == Status.FAILURE ? Status.SUCCESS : status;
        }
    }

    // ----- Services -----

    /** Runs regularly while its node is active, to keep the blackboard up to date. */
    public abstract static class Service {
        private final int interval;
        private long next;

        protected Service(double seconds) {
            this.interval = Math.max(1, ticks(seconds));
        }

        void onStart(Context context) {
            next = context.tick;
        }

        void tick(Context context) {
            if (context.tick >= next) {
                next = context.tick + interval;
                run(context);
            }
        }

        protected abstract void run(Context context);
    }

    public static final class FindNearestEntityService extends Service {
        private final Class<? extends LivingEntity> type;
        private final double range;
        private final boolean mustSee;
        private final String result;

        public FindNearestEntityService(double seconds, Class<? extends LivingEntity> type, double range, boolean mustSee, String result) {
            super(seconds);
            this.type = type;
            this.range = range;
            this.mustSee = mustSee;
            this.result = result;
        }

        @Override
        protected void run(Context context) {
            context.blackboard.set(result, nearest(context, type, range, mustSee));
        }
    }

    public static final class RememberAttacker extends Service {
        private final String result;

        public RememberAttacker(double seconds, String result) {
            super(seconds);
            this.result = result;
        }

        @Override
        protected void run(Context context) {
            LivingEntity attacker = context.mob.getLastHurtByMob();
            if (attacker != null && attacker.isAlive()) {
                context.blackboard.set(result, attacker);
            }
        }
    }

    public static final class UpdateDistance extends Service {
        private final String target;
        private final String result;

        public UpdateDistance(double seconds, String target, String result) {
            super(seconds);
            this.target = target;
            this.result = result;
        }

        @Override
        protected void run(Context context) {
            Vec3 position = context.blackboard.position(target);
            context.blackboard.set(result, position == null ? null : Math.sqrt(context.mob.position().distanceToSqr(position)));
        }
    }

    // ----- Goal -----

    /** Runs the tree on every tick; added to the goals of the mob instead of its list of behaviors. */
    public static final class TreeGoal extends Goal {
        private final Context context;
        private final Node root;

        public TreeGoal(PathfinderMob mob, Node root) {
            this.context = new Context(mob);
            this.root = root;
            this.setFlags(EnumSet.of(Goal.Flag.MOVE, Goal.Flag.LOOK));
        }

        public Blackboard blackboard() {
            return context.blackboard;
        }

        @Override
        public boolean canUse() {
            return root != null && context.mob.isAlive();
        }

        @Override
        public boolean canContinueToUse() {
            return canUse();
        }

        @Override
        public boolean requiresUpdateEveryTick() {
            return true;
        }

        @Override
        public void tick() {
            context.tick++;
            root.tick(context);
        }

        @Override
        public void stop() {
            if (root != null) {
                root.abort(context);
            }
            context.mob.getNavigation().stop();
        }
    }
}

""";
    }
}
