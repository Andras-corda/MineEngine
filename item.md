# Visual
item texture : asset selector
item model : asset selector (par defaut mettre le mode d'extrude de base)
gui only item texture : asset selector (si un model custom et gui only item texture vide alors utiliser le model dans les gui)
has glowing effect : bool
glowing condition : bind to function
name : string
id : string

# Item properties
in game name : string
rarety : liste
creative tabs : asset selector
ma stack size : int (defaut 64)
is imune to fire : bool
is imune to lava : bool
has durability : bool
durability max : int
repair item : asset selector
can destroy any block : bool
does item stay in crafting grid when craft : bool
does item stay in smelting grid when craft : bool
damage item on crafting : bool
damage item on smelting : bool

# Food properties
This item is considered a food : bool (vrai débloque le reste)
nutritional value : int
saturation effect :
eating item result : asset selector
item eating animation : asset selector
item eating duration : float
is always edible : bool
is meat : bool

# tool properties 
This item is considered a tool : bool (vrai débloque le reste)
enchantability : int
durability : int (defaut 500)
repair item : asset selector
type : selector (hache, pioche etc)
tier : selector (diamand, bois, pierre, custom)
efficiency : int
attack speed : float
attack dmg : int

# Advanced properties
## fuel properties
This item is considered a fuel : bool
fuel burn time / power : float
bind function on fuel burn time / power
sucess burning condition : (si pas de bind par défaut en vrai)
bind function on sucess burning condition

## compost properties
This item is considered a compost : bool
sucess compostig condition : (si pas de bind par défaut en vrai)
bind function on sucess compostig condition
compost power : int

## piglin currency
Can be trade with piglin : bool

## bone meal properties
(ne sait pas quoi faire proposer)

## Music disck
(ne sait pas quoi faire proposer)

## Armor trim
(ne sait pas quoi faire proposer)

## Banner pattern
(ne sait pas quoi faire proposer)

# inventory properties
bind item to gui (gui fait plus tard)

# Ranged item properties
This item is considered a ranged item : bool
// 3 types : arc/arbalete/fishing rod
// projectile to shoot
// disable amo check
// Charge power over time
//shoot constantly

# Triggers and events
## basic item :
when right click on block
when right click on entity
when right clicked
when item in hand tick
when item ran out of durability (only if it has durability)
when item broke
when item is crafted/smelted
when item is dropped
when item is picked up

## Food item :
when player start eating
when player stop eating
when player eating tick
when item is enterly consume

## Tool item
when tool is swinging tick
when tool swinged

## Ranged item
When ranged item start charging
When ranged item is charged
When ranged item is charging tick
When ranged item shooted