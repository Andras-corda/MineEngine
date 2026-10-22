# Les types Java utiles pour créer des mods Minecraft

## 1. Objets et inventaires

| Type                     | À quoi ça sert ?                                                  |
| ------------------------ | ----------------------------------------------------------------- |
| `Item`                   | Définir un type d'objet.                                          |
| `ItemStack`              | Représenter un objet avec sa quantité et ses données.             |
| `Items`                  | Accéder aux objets vanilla, comme le diamant.                     |
| `Inventory`              | Représenter un inventaire, selon l'API utilisée.                  |
| `Container`              | Gérer des emplacements d'inventaire dans certaines versions/APIs. |
| `Slot`                   | Représenter un emplacement dans une interface d'inventaire.       |
| `NonNullList<ItemStack>` | Stocker une liste de piles d'objets dans certaines versions.      |
| `EquipmentSlot`          | Identifier un emplacement d'équipement : casque, main, etc.       |
| `InteractionHand`        | Distinguer la main principale et la main secondaire.              |
| `UseAnim`                | Définir une animation d'utilisation d'objet.                      |
| `Rarity`                 | Définir la rareté d'un objet.                                     |

## 2. Blocs et monde

| Type            | À quoi ça sert ?                                                                                                      |
| --------------- | --------------------------------------------------------------------------------------------------------------------- |
| `Block`         | Définir un type de bloc.                                                                                              |
| `BlockState`    | Représenter l'état d'un bloc placé dans le monde.                                                                     |
| `Blocks`        | Accéder aux blocs vanilla.                                                                                            |
| `BlockPos`      | Stocker les coordonnées entières d'un bloc.                                                                           |
| `Level`         | Représenter un monde ou une dimension côté logique.                                                                   |
| `LevelReader`   | Lire des informations du monde.                                                                                       |
| `LevelAccessor` | Accéder au monde avec certaines possibilités de modification.                                                         |
| `ServerLevel`   | Représenter un monde côté serveur.                                                                                    |
| `ClientLevel`   | Représenter le monde côté client.                                                                                     |
| `BlockEntity`   | Stocker des données supplémentaires pour un bloc, par exemple un coffre personnalisé.                                 |
| `BlockGetter`   | Lire des blocs et leurs données.                                                                                      |
| `Direction`     | Représenter nord, sud, est, ouest, haut et bas.                                                                       |
| `FluidState`    | Représenter l'état d'un fluide dans un bloc.                                                                          |
| `Fluid`         | Définir un type de fluide.                                                                                            |
| `Material`      | Concept historique ou propre à certaines APIs, à ne pas confondre avec les systèmes modernes de propriétés des blocs. |

## 3. Entités et joueurs

| Type                                   | À quoi ça sert ?                                                             |
| -------------------------------------- | ---------------------------------------------------------------------------- |
| `Entity`                               | Classe de base des entités du jeu.                                           |
| `LivingEntity`                         | Entité vivante : joueur, zombie, animal…                                     |
| `Player`                               | Joueur, côté logique commun.                                                 |
| `LocalPlayer`                          | Joueur du client local.                                                      |
| `ServerPlayer`                         | Joueur côté serveur.                                                         |
| `Mob`                                  | Entité contrôlée par l'IA.                                                   |
| `PathfinderMob`                        | Créature avec navigation et déplacement par chemin.                          |
| `Animal`                               | Classe de base de nombreux animaux.                                          |
| `Monster`                              | Classe de base de nombreux monstres.                                         |
| `EntityType<T>`                        | Type d'une entité, par exemple un zombie.                                    |
| `DamageSource`                         | Source d'un dégât.                                                           |
| `AttributeInstance`                    | Valeur d'un attribut d'une entité.                                           |
| `EffectInstance` / `MobEffectInstance` | Instance d'un effet, selon la version.                                       |
| `UUID`                                 | Identifiant unique, souvent utilisé pour identifier un joueur ou une entité. |

## 4. Positions, directions et géométrie

| Type                       | À quoi ça sert ?                                                |
| -------------------------- | --------------------------------------------------------------- |
| `BlockPos`                 | Position entière d'un bloc.                                     |
| `Vec3`                     | Position ou vecteur en coordonnées décimales.                   |
| `Vec2`                     | Vecteur 2D.                                                     |
| `Direction`                | Direction cardinale ou verticale.                               |
| `AABB`                     | Boîte englobante utilisée pour les collisions.                  |
| `BlockHitResult`           | Résultat d'un rayon qui touche un bloc.                         |
| `EntityHitResult`          | Résultat d'un rayon qui touche une entité.                      |
| `HitResult`                | Classe de base des résultats de raycast.                        |
| `Quaternionf`              | Rotation 3D, notamment pour le rendu.                           |
| `BlockPos.MutableBlockPos` | Position de bloc mutable pour certaines opérations répétitives. |

## 5. Types Java de base

Ces types sont indispensables, même s'ils ne viennent pas de Minecraft.

| Type           | Utilité                                                  |
| -------------- | -------------------------------------------------------- |
| `int`          | Nombre entier.                                           |
| `double`       | Nombre décimal précis pour les coordonnées et calculs.   |
| `float`        | Nombre décimal souvent utilisé pour le rendu.            |
| `boolean`      | Vrai ou faux.                                            |
| `long`         | Entier sur 64 bits, par exemple pour certains compteurs. |
| `String`       | Texte.                                                   |
| `char`         | Caractère.                                               |
| `byte`         | Entier sur 8 bits.                                       |
| `UUID`         | Identifiant unique Java.                                 |
| `Object`       | Type de base général en Java.                            |
| `Enum`         | Type énuméré, comme une direction ou un mode.            |
| `Optional<T>`  | Valeur éventuellement absente.                           |
| `List<T>`      | Liste ordonnée.                                          |
| `ArrayList<T>` | Implémentation courante d'une liste.                     |
| `Set<T>`       | Ensemble sans doublons.                                  |
| `Map<K,V>`     | Association entre clés et valeurs.                       |
| `HashMap<K,V>` | Implémentation courante d'une map.                       |
| `Consumer<T>`  | Fonction qui reçoit une valeur sans retour.              |
| `Predicate<T>` | Fonction qui teste une condition.                        |
| `Supplier<T>`  | Fonction qui fournit une valeur.                         |

## 6. Interface graphique et menus

| Type                         | À quoi ça sert ?                                              |
| ---------------------------- | ------------------------------------------------------------- |
| `Component`                  | Texte Minecraft avec style et traductions.                    |
| `AbstractContainerMenu`      | Logique d'un menu avec des emplacements.                      |
| `AbstractContainerScreen<T>` | Interface graphique d'un menu.                                |
| `Screen`                     | Écran du client.                                              |
| `GuiGraphics`                | Dessiner des éléments d'interface dans les versions récentes. |
| `Font`                       | Afficher du texte.                                            |
| `Slot`                       | Emplacement d'inventaire dans un menu.                        |
| `MenuType<T>`                | Type d'un menu.                                               |
| `Button`                     | Bouton d'interface, selon l'API et la version.                |
| `Tooltip`                    | Infobulle dans les versions/APIs qui utilisent cette classe.  |

## 7. Événements et interactions

Les noms varient selon Forge, NeoForge et Fabric.

| Type ou concept              | Utilité                                                       |
| ---------------------------- | ------------------------------------------------------------- |
| `Event`                      | Classe de base de nombreux événements Forge/NeoForge.         |
| `InteractionResult`          | Résultat d'une interaction.                                   |
| `InteractionResultHolder<T>` | Résultat d'une interaction accompagné d'une valeur.           |
| `UseOnContext`               | Contexte d'utilisation d'un objet sur un bloc.                |
| `BlockPlaceContext`          | Contexte de placement d'un bloc.                              |
| `BlockHitResult`             | Informations sur le bloc touché.                              |
| `LivingHurtEvent`            | Événement de dégâts dans certaines versions Forge.            |
| `PlayerInteractEvent`        | Événement d'interaction dans Forge.                           |
| `TickEvent`                  | Événements de tick dans certaines versions Forge.             |
| `ActionResult`               | Résultat d'interaction dans plusieurs APIs, notamment Fabric. |

Les événements ne sont pas identiques entre les mod loaders : Fabric utilise souvent des callbacks et des événements spécifiques à ses API, alors que Forge et NeoForge possèdent leurs propres systèmes d'événements.

## 8. Données, sauvegarde et réseau

| Type                              | À quoi ça sert ?                                                         |
| --------------------------------- | ------------------------------------------------------------------------ |
| `CompoundTag`                     | Stocker des données NBT dans les versions qui utilisent cette API.       |
| `ListTag`                         | Stocker une liste de données NBT.                                        |
| `Tag`                             | Classe de base des données NBT dans les anciennes APIs.                  |
| `DataComponentType<T>`            | Définir un type de donnée attaché à un objet dans les versions modernes. |
| `FriendlyByteBuf`                 | Lire et écrire des données réseau dans certaines versions.               |
| `CustomPacketPayload`             | Représenter certains paquets réseau dans les versions modernes.          |
| `SavedData`                       | Sauvegarder des données personnalisées d'un monde.                       |
| `Codec<T>`                        | Sérialiser et désérialiser des données.                                  |
| `ResourceLocation` / `Identifier` | Identifier une ressource avec un namespace et un nom, selon la version.  |
| `ResourceKey<T>`                  | Identifier une entrée d'un registre ou une ressource typée.              |

## 9. Blocs spéciaux, recettes et contenu du mod

| Type                        | À quoi ça sert ?                                                       |
| --------------------------- | ---------------------------------------------------------------------- |
| `BlockEntity`               | Données supplémentaires d'un bloc, comme l'inventaire d'une machine.   |
| `BlockEntityType<T>`        | Type d'une block entity.                                               |
| `BlockItem`                 | Objet permettant généralement de placer un bloc.                       |
| `BlockBehaviour.Properties` | Configurer les propriétés d'un bloc dans les APIs modernes concernées. |
| `Item.Properties`           | Configurer un objet.                                                   |
| `Recipe<T>`                 | Définir une recette, selon la version et les génériques disponibles.   |
| `RecipeType<T>`             | Catégorie de recette.                                                  |
| `RecipeSerializer<T>`       | Lire et enregistrer une recette.                                       |
| `Ingredient`                | Ingrédients possibles d'une recette.                                   |
| `CreativeModeTab`           | Onglet créatif dans les versions récentes.                             |
| `SoundEvent`                | Son enregistré dans le jeu.                                            |
| `ParticleOptions`           | Paramètres d'une particule.                                            |
| `MobEffect`                 | Définition d'un effet de statut.                                       |
| `Attribute`                 | Définition d'un attribut.                                              |
| `DeferredRegister`          | Enregistrement différé utilisé notamment par Forge/NeoForge.           |

## 10. Client, serveur et rendu

| Type                     | À quoi ça sert ?                                             |
| ------------------------ | ------------------------------------------------------------ |
| `Minecraft`              | Instance principale du client dans de nombreuses versions.   |
| `MinecraftServer`        | Serveur Minecraft.                                           |
| `ServerLevel`            | Monde côté serveur.                                          |
| `ClientLevel`            | Monde côté client.                                           |
| `PoseStack`              | Gérer les transformations du rendu dans les APIs concernées. |
| `VertexConsumer`         | Envoyer des sommets au système de rendu.                     |
| `RenderType`             | Définir un type de rendu.                                    |
| `EntityRenderer<T>`      | Rendre une entité.                                           |
| `BlockEntityRenderer<T>` | Rendre une block entity.                                     |
| `ModelPart`              | Partie d'un modèle 3D dans les systèmes concernés.           |
| `KeyMapping`             | Définir une touche personnalisée dans certaines versions.    |
| `Particle`               | Représenter une particule côté client.                       |
