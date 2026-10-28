# Visual
name : string
registry name : string

Mettre les textures
|                                |                                 |                                |                                 |
| ------------------------------ | ------------------------------- | ------------------------------ | ------------------------------- |
|                                | Top                             |                                |                                 |
| left                           | Front                           | Right                          | Back                            |
|                                | Bottom                          |                                |                                 |

Item texture : assets selector
particle texture : assets selector

block base : sert à dééfinir un model de base (cube pour block classic, fence, gate, door, escalier, dalle etc)

has custom modele : boolean
modele :  assets selector

rotation mode : (mode de rotation du block comme en fonction du pitch du joueur etc)
has pitch rotation : boolean

has transparency : boolean (mettre vrai active transparence type, custom light opacity)
transparence type : (cutout par exemple)

custom light opacity : boolean (mettre vrai active opacity)
opacity : int

is glowing : boolean (mettre vrai active luminescence factor)
luminescence factor : int


use default bounding box : boolean (en fonction de block base)
custom bounding box : coordonné de depart x y z et fin x y z (exemple x:0 y:0 z:0 et x:16 y:16 z:16 pour un cube classic) (sous forme de liste avec ajouts pour pouvoir créer deux bounding box séparer qui fusionnent)

# Texture properties // Expérimental pour plus tard
Sert à faire en sorte d'avoir des block avec des textures qui se connectent et/ou des variantes de block avec des color map ou des textures spéciales

# Block properties
hardness : int
resitence : int
is unbreakable : boolean (mettre vrai désactive hardness et resistence)
is replaceable : boolean (like grass or certain plant)

enable tick rate : boolean (mettre vrai active tick randomly et tick rate)
tick randomly : boolean (mettre vrai désactive tick rate)
tick rate : int

has sliperiness : boolean (mettre vrai active slipperiness)
slipperiness : int

has specific jump factor : boolean (mettre vrai active jump factor)
jump factor : int

has specific speed factor : boolean (mettre vrai active speed factor)
speed factor : int

enchantment power : int

can plant sustain : boolean (mettre vrai active plant list)
plant list : asset selector si (si vide toute les plantes)

block color on map : hexadecimal
note block instrument : liste des instruments
beacon color modifier : hexadecimal

reaction to being pushed : boolean (mettre vrai active l'event associé (il apparait dans la liste des events))
block has gravity : boolean

can block be flammed : boolean (mettre vrai active fire speading speed)
can block be ignited by lava : boolean
fire speading speed : int

# Block item properties
Does block have an item : boolean (par défaut vrai et si faux alors désactive Item texture dans visual, Max stack size, rarety, is imune to fire, is imune to lava et creative tab)
Max stack size : int (par défaut 64)
rarety : list des rareté minecraft
is imune to fire : boolean
is imune to lava : boolean
creative tab : asset selector des onglets creatifs

# Redstone properties
Does redstone connect to block : boolean

does emit redstone factor : boolean (mettre vrai active redstone power)
redstone power : int (de 0 à 16)

# Block entity properties // Expérimental pour plus tard
enable block entity : boolean (mettre vrai active toute la cathégorie block entity)

bind to gui : asset selector d'interface de jeu
drop inventory when break : boolean

# Drop properties
Custom drop : boolean (si faux alors ça drop le block, si vrai active )
Amounth : int
bind to drop table : asset selector drop table
does drop experiences : boolean (si vrai active experience amount)
experience amount : (fixed : int ou range avec min et max en int)

require correct tools to drop : boolean
tool by type or specific : asset selector (type d'outil comme hache, pioche etc ou un outil specifique du style que la pioche en diamand)
Vanilla tool tier : liste (bois, fer diamand etc)
additional condition : bind sur une fonction

# Triggers and events
- OnBlockRightlick
- WhenBlockAdded
- WhenNeighbourChanged
- OnTickUpdate(si tick rate est activé)
- WhenBlockDestroyed
- WhenBlockDestroyedByPlayer
- WhenBlockDestroyedByExplosion
- WhenBlockStartDestroy
- WhenEntityCollides
- WhenPlayerCollides
- WhenEntityWalksOn
- WhenPlayerWalksOn
- WhenEntityFallOnBlock
- WhenBlockHitByProjectile
- WhenBlockIsPlaced
- RedstoneOn
- RedstoneOff

# Generation
Ore generation : boolean (mettre vrai active block will be replaced, biomes, generation shape, height selection, group per chunk, ore in groupe)
block will be replaced : asset selector multiple

biomes : assets selector multiple (si vide pas de restriction)
generation shape : forme de generation
height selection : min et max (range int)
group per chunk : int
ore in groupe : int