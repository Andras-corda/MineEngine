Entity est décomposé en fenêtres qui permettent de moduler directement.

# Visual : 
entity name : champ text
entity id : champ text (en minuscule et séparé par des _)

Model : case avec sélecteur d'assets
model visual scale : selecteur[fixed|dynamique] (si dynamique possibilité de link une fonction/un graph)

Texture (layer0) : case avec sélecteur d'assets
texture has transparancy : boolean

## Bounding box

bounding box : 4 champs float (This parameter controls the size of your mob's hitbox in blocks, as well as the size of its shadow.
The first option sets the width and depth, whilst the second option sets the height.
The third option sets size in blocks for the radius of the mob's shadow.
The fourth option sets the Y offset of the passenger's attachment point on the entity it's riding (this entity). In other words, this parameter defines where another entity will be attached to this entity when riding it.)

disable collisions : boolean

bounding box scale : selecteur[fixed|dynamique] (si dynamique possibilité de link une fonction/un graph)

# Variations and Layers

has variations : boolean
has many layers : boolean

// Sous forme de liste

enregistrer les variant comme nbt
layer (x) glow : boolean
disable hurt on layer (x) : boolean

bind à une fonction/graph pour le display du layer, le glow et le hurt effect

# Attrubute

## Base

charactéristique : selecteur [Mob,Creature,Raider,custom]
Type : selecteur [Undefined,Undead,Arthropod,Illager,Water,custom]

(terme pouvoir creer un nouvel entity type)

Movement speed : champ nombre
step height : champ nombre
follow range : champ nombre
traking range : champ nombre

Life : champ nombre
knockback : champ nombre
knockback resistence : champ nombre
strenght attack : champ nombre
armor : champ nombre

## Tamable entity
(items et probability sont visible seulement si tamable est activé)
tamable : boolean
items : case avec sélecteur d'assets (plusieurs)
probability per item : affichage en liste avec proba en float
exemple : bones :33% (par os)

## Ridable entity
(les champs sont visible seulement si ridable est activé)
ridable : boolean
forward movement : boolean
strafe movement : boolean
need item : boolean
layer ref : reference au layer sur lequel se trouve la scelle

## Water entity
(les champs sont visible seulement si is water entity est activé)
is water entity : boolean
can breath under fluid : boolean (bind à une fonction/graph)
can be pushed by fluid : boolean (bind à une fonction/graph)

fluid : case avec selecteur d'asset

// à faire plus tard
## Flying entity
(les champs sont visible seulement si is flying entity est activé)

## Imunity and vulnearbility
is imune to : selecteur multiple [fire, lava, lightning, arrows, fall damage, player melee,drowning,trident, potions, cactus, fall damage, explosion, anvil, dragon breath, wither, custom damage type]

is vulnarable to selecteur multiple [sun, moon, fluid (custom or minecraft), potions, custom damage type]

# spawn
enable spawn entity : boolean
enable despawn : boolean
custom condition to despawn : bind à une fonction/graph si vide alors normal
spawn weight : champ nombre
spawn group weight : selecteur [fixed,range,dynamic] si fixed champ => nombre, si range => deux champ nombre (min et max), si dynamic bind à une fonction/graph

is attached to structure : boolean
structure : selecteur multiple

dimensions : selection multiple, ne peut etre vide et par défaut l'overworld
biomes : selection multiples si vide alors pas de restriction depend des dimensions

## spawn egg

color a et color b : color picker / hexadecimal
creative tabs : selecteur assets pour les creative tabs

custom texture : assets selector

# special properties
## boss
(les champs sont visible seulement si is boss est activé)
is boss : boolean
show healthbar : boolea
color : color picker + hexa
type : progressbar
customProgressBar : bind sur un widget component (à faire dans d'autres version ne marche pas mtn)

## golem
(les champs sont visible seulement si is golem est activé)
is golem : boolean
block activator : selecteur assetqs (par défaut citrouille craved)
dection function : function/graph bind sur le block sélectionné pour prendre en compte cette sélection pour faire apparaitre le golem

## Vehicule
(les champs rarety et item textures sont grisé par défaut et se débloque si is boat ou is a minecart est vrai, on ne peut pas faire les deux vrai, si un des deux est vrai alors grise, la section spawn pour empecher le spawn, les oeufs etc)
is a boat : boolean
is a minecart : boolean

rarety : selecteur de rareté
item texture : selecteur assets

# Animation / Sounds et state machine
a faire plus tard nouveau type de nodal pour gérer les animation et leurs déclenchement seulont des conditions + gestion de sons

# inventory
has gui : boolean
widget link : plus tard nouveau type : editeur xaml d'interface minecraft/overlay comme widget bp

# behavior tree puppeteers
pouvoir lier un puppeteer externe ou en creer un dans cette fenetre
celui crée peut etre exporté en public ou rester private

# events and triggers
vue en graph editor avec différents events inutilisé par défaut :
- when it's struck by lightning
- when it falls
- when it spawn(do once)
- when it dies(do once)
- when player collides
- tick update
- when it's hurt
- right click by player
- when it kills another entity
- when ride start(do once)
- whenb ride stop(do once)
- Tick update when ride
- when tamed(do once)
- when entity collide