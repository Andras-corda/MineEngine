# 04. Modèle du domaine

Ce document décrit les données d'un mod : identifiants, éléments, propriétés, bornes, valeurs par défaut et règles de validation. Les graphes ont leur propre document ([05](05-programmation-nodale.md)) ; leur forme sur le disque est dans [06](06-formats-de-fichiers.md).

## 1. Identifiants

### Identifiant du mod (`ModId`)

- Une lettre minuscule, puis uniquement des lettres minuscules `a-z`, des chiffres ou `_`. De 2 à 64 caractères (exigence de NeoForge).
- Choisi à la création du projet, **jamais modifiable ensuite** (il est inscrit dans tous les identifiants du jeu et dans les mondes déjà joués).
- Proposé à partir du nom du mod par la règle de nettoyage ci-dessous ; à défaut, `mymod`.

### Identifiant en jeu d'un élément (`ResourceId`)

- Même alphabet, de 1 à 64 caractères. C'est la partie `magic_sword` de `monmod:magic_sword`.
- Renommable à tout moment. Les liens entre éléments ne passent pas par lui.
- Proposé à partir d'un texte libre par la règle de nettoyage ; à défaut, `unnamed`.

### Règle de nettoyage d'un texte libre en identifiant

1. Décomposer les caractères accentués (forme Unicode NFD) et retirer les marques diacritiques : « Épée » devient « Epee ».
2. Passer en minuscules.
3. Garder les lettres `a-z` et les chiffres ; remplacer chaque suite d'autres caractères par un seul `_` (jamais en tête).
4. Retirer les `_` en début et en fin.
5. Si le résultat commence par un chiffre, le préfixer par `id_`.
6. Tronquer à la longueur maximale (en retirant un `_` final éventuel).
7. Si le résultat reste invalide, utiliser l'identifiant de secours.

Exemples : « Épée magique » donne `epee_magique` ; «   Ruby   Block!! » donne `ruby_block` ; « 42 blocs » donne `id_42_blocs` ; « ??? » donne `unnamed`.

### Identifiant unique automatique

Pour un nouvel élément, l'identifiant proposé (`new_item`...) est rendu unique en ajoutant `_2`, `_3`, etc. Le suffixe raccourcit la base si nécessaire pour rester dans les 64 caractères.

### Identifiant complet (`NamespacedId`)

- Format `espace:chemin`. Sans `:`, l'espace est `minecraft`.
- Espace : minuscules, chiffres, `_`, `-`, `.`. Chemin : les mêmes plus `/`.

### Identifiant interne

Un UUID par élément, généré à la création, jamais modifié. C'est la seule façon pour un élément d'en désigner un autre.

### Espaces d'unicité

Deux éléments ne peuvent pas avoir le même identifiant en jeu dans le même espace :

| Espace | Sortes d'éléments |
| --- | --- |
| Contenu du jeu | items, blocs et mobs (ils partagent les registres et l'inventaire) |
| Recettes | recettes |
| Textures | textures |
| Sons | sons |
| Graphes | graphes |

Une texture peut donc porter le même nom que l'item qui l'utilise. L'œuf d'un mob (`<mob>_spawn_egg`) est un item : il ne doit pas entrer en collision avec un autre élément du contenu du jeu.

## 2. Références de contenu

Une **référence de contenu** désigne un item, un bloc ou un son. Elle a trois formes, et un texte qui la représente sur le disque :

| Forme | Exemple de texte | Usage |
| --- | --- | --- |
| Élément du projet | `asset:3f2b...-...` (UUID) | suit l'élément même s'il est renommé |
| Élément du jeu ou d'un autre mod | `minecraft:diamond` | identifiant complet |
| Tag d'items | `#minecraft:planks` | seulement comme ingrédient de recette |

Un texte vide ou illisible équivaut à « aucune référence ».

## 3. L'élément de base

Tout élément possède :

| Propriété | Description |
| --- | --- |
| Identifiant interne | UUID, non vide |
| Sorte | `Item`, `Block`, `Mob`, `Recipe`, `Texture`, `Sound`, `Graph` |
| Identifiant en jeu | voir plus haut |
| Nom affiché | texte libre, peut être vide (l'identifiant est alors utilisé en jeu) |
| Dossier | chemin logique du Dashboard (`""` pour la racine) |

Chaque élément sait :

- **se valider** et ajouter ses problèmes à une liste de diagnostics ;
- **lister ses liens** vers d'autres éléments (`AssetReference` : élément visé, sorte attendue, rôle lisible comme « texture » ou « item laissé ») ;
- **signaler chacune de ses modifications**.

Règle commune : un nom affiché vide donne un avertissement (sauf pour un graphe, dont le nom ne sert qu'à l'éditeur).

### Sorte attendue d'un lien

Un lien précise ce qu'il attend : une texture, un item (un item, ou un bloc qui a une forme item), un son, un bloc, ou une définition (un graphe). Le registre vérifie chaque lien :

- élément visé supprimé : « l'asset visé a été supprimé » ;
- mauvaise sorte : « X n'est pas une texture / un son / un bloc / un item / une définition » ;
- bloc sans forme item visé comme item : « le bloc X n'a pas de forme item ».

**Une texture manquante n'est qu'un avertissement** (une texture de remplacement sera utilisée). Tout autre lien cassé est une erreur qui empêche de générer.

## 4. Propriétés d'inventaire (items et forme item des blocs)

| Propriété | Défaut | Bornes / règles |
| --- | --- | --- |
| Texture principale | aucune | lien vers une texture ; absente : avertissement |
| Taille de pile | 64 | 1 à 99 |
| Rareté | `Common` | `Common`, `Uncommon`, `Rare`, `Epic` (couleur du nom) |
| Résiste au feu et à la lave | non | |
| Lignes d'info-bulle | aucune | lignes vides retirées, espaces aux bords retirés |
| Dans l'onglet créatif du mod | oui | |
| Onglets créatifs vanilla | aucun | ensemble parmi `BuildingBlocks`, `ColoredBlocks`, `NaturalBlocks`, `FunctionalBlocks`, `RedstoneBlocks`, `ToolsAndUtilities`, `Combat`, `FoodAndDrinks`, `Ingredients`, `SpawnEggs` |

Validation : si l'élément a une forme item, n'est pas dans l'onglet du mod et dans aucun onglet vanilla, une **info** le signale (il ne sera accessible que par commande ou recette).

## 5. Item

En plus des propriétés d'inventaire :

### Type d'item

| Propriété | Défaut | Bornes / valeurs |
| --- | --- | --- |
| Sorte (`Kind`) | `Simple` | `Simple`, `Sword`, `Pickaxe`, `Axe`, `Shovel`, `Hoe`, `Helmet`, `Chestplate`, `Leggings`, `Boots` |
| Matériau d'outil (`Tier`) | `Iron` | `Wood`, `Stone`, `Iron`, `Gold`, `Diamond`, `Netherite` |
| Matériau d'armure | `Iron` | `Leather`, `Chainmail`, `Iron`, `Gold`, `Diamond`, `Turtle`, `Netherite` |
| Dégâts ajoutés | 3 | -100 à 100 |
| Vitesse d'attaque | -2,4 | -4 à 100 |
| Tenu comme un outil (`Handheld`) | non | modèle « en diagonale » |

Les outils (épée, pioche, hache, pelle, houe) et les armures sont des **équipements** : ils ne s'empilent pas et tirent leur durabilité de leur matériau. Quand on change de sorte d'outil, l'éditeur propose les valeurs de Minecraft pour un outil en fer :

| Sorte | Dégâts | Vitesse |
| --- | --- | --- |
| Épée | 3 | -2,4 |
| Pioche | 1 | -2,8 |
| Hache | 6 | -3,1 |
| Pelle | 1,5 | -3 |
| Houe | -2 | -1 |

### Propriétés

| Propriété | Défaut | Bornes |
| --- | --- | --- |
| Durabilité | 0 (aucune) | 0 à 100 000 |
| Effet brillant (`glint`) | non | |

### Nourriture (si l'item est comestible)

| Propriété | Défaut | Bornes |
| --- | --- | --- |
| Valeur nutritive | 4 | 0 à 20 (demi-cuisses) |
| Saturation | 0,3 | 0 à 10 |
| Toujours mangeable | non | |
| Viande (les loups la mangent) | non | |
| Se mange vite | non | |
| Effets | aucun | liste d'effets (voir ci-dessous) |

Un effet de nourriture : sorte d'effet (`Speed`, `Slowness`, `Haste`, `MiningFatigue`, `Strength`, `InstantHealth`, `InstantDamage`, `JumpBoost`, `Nausea`, `Regeneration`, `Resistance`, `FireResistance`, `WaterBreathing`, `Invisibility`, `Blindness`, `NightVision`, `Hunger`, `Weakness`, `Poison`, `Wither`, `HealthBoost`, `Absorption`, `Saturation`, `Glowing`, `Levitation`, `Luck`, `SlowFalling`), durée de 1 à 3600 secondes (défaut 5), niveau de 0 à 9 (0 = I), probabilité de 0 à 1 (défaut 1).

### Données

| Propriété | Défaut | Bornes |
| --- | --- | --- |
| Item rendu après fabrication | aucun | référence d'item (pas un tag) |
| Non réparable à l'enclume | non | |
| Durée de combustion | 0 (pas un combustible) | 0 à 72 000 ticks |
| Tags d'items | aucun | identifiants complets, sans doublon |

### Événements d'item

Les sept événements d'item (clic droit, utilisé sur un bloc, frappe une créature, bloc cassé avec, mangé ou bu, fabriqué, tenu en main) ne sont pas des propriétés de l'item : ce sont des nœuds d'un graphe d'événements dont l'entrée « item » désigne cet item. L'éditeur de l'item les liste et permet d'en ajouter (voir [05](05-programmation-nodale.md)).

### Validation de l'item

- Équipement avec une durabilité saisie : **info** (elle est ignorée).
- Équipement avec une taille de pile supérieure à 1 : **info** (elle sera 1).
- Item simple avec durabilité et taille de pile supérieure à 1 : **avertissement** (elle sera 1).
- Item rendu après fabrication qui est un tag : **erreur**.
- Même effet de nourriture donné plusieurs fois : **info** (Minecraft n'applique que le plus fort).

Liens : texture principale, item rendu après fabrication s'il est un élément du projet.

## 6. Bloc

En plus des propriétés d'inventaire (qui ne s'appliquent que si le bloc a une forme item) :

### Visuel

| Propriété | Défaut | Valeurs |
| --- | --- | --- |
| Forme (`Model`) | `Cube` | `Cube` (une texture), `CubeFaces` (une texture par face), `Column` (orientable comme une bûche), `Cross` (deux plans en croix, comme une fleur) |
| Transparence (`RenderType`) | `Solid` | `Solid`, `Cutout`, `CutoutMipped`, `Translucent` |
| Lumière émise | 0 | 0 à 15 |
| Textures par emplacement | aucune | `Top`, `Bottom`, `North`, `South`, `East`, `West` ; un emplacement vide reprend la texture principale |

Emplacements utilisés selon la forme : `Cube` et `Cross` utilisent la principale ; `Column` utilise la principale (côtés) et `Top` (extrémités) ; `CubeFaces` utilise les sept.

### Propriétés

| Propriété | Défaut | Bornes |
| --- | --- | --- |
| Dureté | 1,5 | 0 à 3 600 000 |
| Résistance aux explosions | 6 | 0 à 3 600 000 |
| Incassable | non | |
| Sons | `Stone` | `Stone`, `Wood`, `Gravel`, `Grass`, `Metal`, `Glass`, `Sand`, `Wool`, `Snow`, `Deepslate`, `Amethyst`, `Copper`, `Netherrack`, `Moss` |
| Glissance | 0,6 | 0,1 à 1,2 |
| Vitesse de marche | 1 | 0,1 à 5 |
| Hauteur de saut | 1 | 0,1 à 5 |

### Récolte

| Propriété | Défaut | Valeurs / bornes |
| --- | --- | --- |
| Outil efficace | `None` | `None`, `Pickaxe`, `Axe`, `Shovel`, `Hoe` |
| Niveau d'outil | `Any` | `Any`, `Stone`, `Iron`, `Diamond` |
| Outil requis pour laisser quelque chose | non | |
| Laisse tomber | `Self` | `Self`, `Nothing`, `OtherItem` |
| Item laissé | aucun | référence d'item (si `OtherItem`) |
| Quantité laissée | 1 à 1 | 1 à 64 chacune |
| Expérience donnée | 0 à 0 | 0 à 1000 chacune |
| Forme item | oui | |

### Validation du bloc

- Outil requis coché sans outil choisi : **avertissement** (il ne laissera jamais rien).
- Niveau d'outil sans outil : **avertissement** (ignoré).
- `OtherItem` sans item : **erreur**. Avec un tag : **erreur**.
- `Self` sans forme item : **avertissement** (il ne laissera rien).
- Minimum supérieur au maximum (quantité ou expérience) : **avertissement** (ils seront inversés).
- Bloc `Column` qui donne de l'expérience : **avertissement** (ignorée).
- Bloc `Cross` en `Solid` : **info** (choisir « Découpe »).

Liens : texture principale, textures d'emplacements (rôle « texture top »...), item laissé s'il est un élément du projet.

## 7. Mob

### Apparence

| Propriété | Défaut | Valeurs / bornes |
| --- | --- | --- |
| Modèle | `Zombie` | `Zombie`, `Skeleton`, `Villager`, `Witch`, `Creeper`, `Spider`, `Cow`, `Pig`, `Chicken`, `Ocelot`, `Blaze`, `Slime`, `Silverfish`, `SnowGolem` |
| Texture | aucune | absente : celle de la créature d'origine (avertissement adapté) |
| Taille personnalisée | non | sinon, taille de la créature d'origine (tableau ci-dessous) |
| Largeur, hauteur | 0,6 et 1,8 | 0,1 à 16 blocs |

Taille d'origine des modèles (largeur × hauteur) : Zombie, Villager, Witch 0,6 × 1,95 ; Skeleton 0,6 × 1,99 ; Creeper 0,6 × 1,7 ; Spider 1,4 × 0,9 ; Cow 0,9 × 1,4 ; Pig 0,9 × 0,9 ; Chicken 0,4 × 0,7 ; Ocelot 0,6 × 0,7 ; Blaze 0,6 × 1,8 ; Slime 1,02 × 1,02 ; Silverfish 0,4 × 0,3 ; SnowGolem 0,7 × 1,9.

### Attributs

| Propriété | Défaut | Bornes |
| --- | --- | --- |
| Famille (`Kind`) | `Monster` | `Monster` (hostile, apparaît dans le noir), `Creature` (neutre ou passive), `Animal` (reproduction, bébés) |
| Points de vie | 20 | 1 à 1024 |
| Armure | 0 | 0 à 30 |
| Vitesse | 0,25 | 0 à 2 |
| Dégâts d'attaque | 3 | 0 à 2048 |
| Portée de suivi | 16 | 1 à 128 |
| Résistance au recul | 0 | 0 à 1 |
| Expérience donnée | 5 | 0 à 1000 |
| Résiste au feu | non | |
| Brûle au soleil | non | |
| Ne disparaît jamais | non | |

### IA : comportements ordonnés

L'IA est une liste ordonnée de comportements : le premier qui peut agir l'emporte sur les suivants. Un comportement a une sorte et cinq réglages (vitesse 0 à 3, distance 0 à 64, cible, item, option), dont chaque sorte n'utilise qu'une partie :

| Sorte | Sélecteur | Réglages utilisés (défaut) |
| --- | --- | --- |
| `Swim` (nager) | action | aucun |
| `Panic` (paniquer quand il est blessé) | action | vitesse (1,25) |
| `MeleeAttack` (attaquer au corps à corps) | action | vitesse (1,2), option « continue sans voir la cible » |
| `LeapAtTarget` (bondir sur la cible) | action | distance = hauteur du bond (0,4) |
| `RandomStroll` (se promener) | action | vitesse (1) |
| `LookAtPlayer` (regarder les joueurs) | action | distance (8) |
| `RandomLookAround` (regarder autour de lui) | action | aucun |
| `Tempt` (suivre un joueur qui tient un item) | action | vitesse (1,2), item |
| `Breed` (se reproduire) | action, **Animal seulement** | vitesse (1) |
| `FollowParent` (suivre ses parents) | action, **Animal seulement** | vitesse (1,1) |
| `AvoidEntity` (fuir une créature) | action | vitesse (1,2), distance (8), cible |
| `FleeSun` (fuir le soleil) | action | vitesse (1) |
| `MoveTowardsTarget` (se rapprocher de la cible) | action | vitesse (0,9), distance (32) |
| `HurtByTarget` (riposter) | ciblage | option « appelle ses semblables à l'aide » |
| `AttackNearest` (cibler les créatures proches) | ciblage | cible, option « doit voir la cible » (oui) |

Cibles possibles : `Player`, `Villager`, `IronGolem`, `Animal`, `Monster` (défaut `Player`).

IA de départ selon la famille :

- **Monster** : nager, attaquer au corps à corps, se promener, regarder les joueurs, regarder autour, riposter, cibler les créatures proches.
- **Animal** : nager, paniquer, se reproduire, suivre ses parents, se promener, regarder les joueurs, regarder autour.
- **Creature** : nager, paniquer, se promener, regarder les joueurs, regarder autour.

| Propriété | Défaut |
| --- | --- |
| Item de reproduction (Animal) | aucun |

### Sons et butin

| Propriété | Défaut |
| --- | --- |
| Son ambiant, de blessure, de mort | aucun (son par défaut de la famille) ; référence de son |
| Butin | aucun ; liste de (item, minimum, maximum), quantités de 0 à 64 |

### Apparition

| Propriété | Défaut | Bornes |
| --- | --- | --- |
| Œuf d'apparition | oui | identifiant `<mob>_spawn_egg` |
| Couleur de fond de l'œuf | `#4C7A2A` | RGB |
| Couleur des taches | `#1F2E14` | RGB |
| Apparition naturelle | non | |
| Biomes | `Overworld` | ensemble parmi `Overworld`, `Plains`, `Forest`, `Taiga`, `Jungle`, `Savanna`, `Desert`, `Swamp`, `Mountain`, `Badlands`, `Beach`, `Ocean`, `River`, `Nether`, `End` |
| Fréquence (poids) | 10 | 1 à 1000 |
| Taille des groupes | 1 à 3 | 1 à 16 chacune |

### Validation du mob

- Comportement réservé aux animaux sur un autre mob : **erreur**.
- Comportement qui utilise un item sans item : **erreur** ; avec un tag : **erreur**.
- Comportement présent plusieurs fois : **info**.
- Attaque au corps à corps sans comportement de ciblage : **avertissement**.
- Ciblage sans attaque, sur un monstre : **info**.
- Animal avec « se reproduire » mais sans item de reproduction : **avertissement**. Item de reproduction qui est un tag : **erreur**.
- Son qui est un tag : **erreur**. Butin qui contient un tag : **erreur**.
- Apparition naturelle sans biome : **avertissement**. Groupe minimum supérieur au maximum : **avertissement**.
- Œuf d'apparition dont l'identifiant dépasserait 64 caractères (identifiant + 10) : **erreur**.
- Brûle au soleil mais résiste au feu : **info**.

Liens : texture, items des comportements, item de reproduction (Animal), sons, items du butin.

## 8. Recette

| Propriété | Défaut | Bornes / valeurs |
| --- | --- | --- |
| Sorte | `Shaped` | `Shaped` (établi, disposition imposée), `Shapeless` (établi, disposition libre), `Smelting` (four) |
| Grille | 9 cases vides | 3 × 3, ligne par ligne ; chaque case est une référence (item, élément ou tag) ou vide |
| Ingrédient à cuire | aucun | référence (four) |
| Résultat | aucun | référence d'item (pas un tag) |
| Quantité produite | 1 | 1 à 64 (établi ; le four en produit toujours un) |
| Expérience | 0,1 | 0 à 100 (four) |
| Durée de cuisson | 200 ticks | 1 à 72 000 (four) |

Les ingrédients utilisés sont l'ingrédient à cuire pour un four, sinon les cases remplies de la grille. Une recette sans disposition ne tient pas compte de la position des cases.

Validation : pas de résultat ou résultat qui est un tag : **erreur** ; aucun ingrédient : **erreur** (« Choisissez l'ingrédient à cuire » ou « La grille de l'établi est vide »).

Liens : ingrédients et résultat qui sont des éléments du projet.

## 9. Texture et son

| Élément | Propriétés | Validation |
| --- | --- | --- |
| Texture | fichier PNG, chemin relatif au projet avec des `/` | aucun fichier : **erreur** |
| Son | fichier OGG (de même), sous-titre (texte, peut être vide) | aucun fichier : **erreur** |

Ces éléments sont créés par import (voir [06](06-formats-de-fichiers.md) pour la copie et le nommage des fichiers).

## 10. Le registre des éléments

Le registre est la liste ordonnée des éléments d'un projet. Il :

- refuse un ajout dont l'identifiant interne existe déjà, ou dont l'identifiant en jeu est pris dans le même espace ;
- permet de réinsérer un élément à une position précise (pour annuler une suppression sans changer l'ordre) ;
- retrouve un élément par identifiant interne, ou par identifiant en jeu dans un espace ;
- liste les éléments qui en désignent un autre, avec le rôle de chaque lien (« Utilisé par ») ;
- relaie les modifications de chaque élément.

**Validation complète d'un projet**, dans cet ordre :

1. Identifiants en double dans un même espace (un fichier modifié à la main peut en créer) : **erreur**.
2. Œufs d'apparition en collision avec un autre élément du contenu du jeu : **erreur**.
3. Chaque élément se valide. Un graphe se valide avec toutes les définitions du projet (voir [05](05-programmation-nodale.md)).
4. Chaque lien de chaque élément est vérifié (voir la section 3).

## 11. Paramètres du projet

| Paramètre | Défaut | Règles |
| --- | --- | --- |
| Identifiant du mod | proposé depuis le nom | voir la section 1, figé après la création |
| Nom du mod | | vide : l'identifiant ; obligatoire dans le formulaire |
| Version du mod | `1.0.0` | obligatoire, sans espace |
| Auteurs | vide | |
| Description | vide | |
| Licence | `All Rights Reserved` | obligatoire |
| Site web | vide | vide, ou commence par `https://` |
| MDK | aucun | identifiant d'un MDK installé (`forge-1.20.1-47.4.10`) ; il fixe le loader et la version |
| Version de Minecraft, loader | | recopiés depuis le MDK choisi |
| Dossiers déclarés | aucun | chemins normalisés, sans doublon, triés |
| Fichiers verrouillés | aucun | chemins relatifs au projet avec des `/`, sans doublon (comparaison insensible à la casse), triés |

## 12. Dossiers du Dashboard

Les dossiers sont des chemins logiques (`Armes/Épées`), séparés par `/`. Ils ne déplacent aucun fichier.

- **Nom valide** : non vide, sans espace en début ou en fin, sans `/` ni `\ : * ? " < > |`, différent de `.` et `..`, 64 caractères au plus.
- **Normalisation d'un chemin** : segments rognés, segments vides retirés ; un nom invalide rend le chemin invalide.
- **Dossiers existants** : les dossiers déclarés dans les paramètres (ce qui permet des dossiers vides), plus ceux qui contiennent un élément, plus tous leurs parents.
- **Créer** : ajoute le chemin aux dossiers déclarés.
- **Renommer ou déplacer** : le dossier, ses sous-dossiers et tous les éléments qu'ils contiennent suivent. Un dossier ne peut pas être déplacé dans lui-même.
- **Supprimer** : le dossier disparaît, son contenu (éléments et sous-dossiers) remonte d'un niveau. Rien n'est supprimé.
- **Déplacer des éléments** vers un dossier.

Chacune de ces opérations est une seule commande annulable.

## 13. Projets connus

La liste de l'accueil contient, pour chaque projet, son dossier et la date de sa dernière ouverture (ou rien s'il n'a jamais été ouvert). Un même dossier n'y figure qu'une fois (comparaison insensible à la casse, la plus récente ouverture l'emporte). Au premier lancement, la liste est remplie à partir des projets trouvés dans le dossier par défaut (`Documents\MineEngine Projects`).

Un aperçu de chaque projet est lu sans charger le projet : ses paramètres, le nombre d'éléments par sorte (lu dans chaque fichier), et quelques textures pour la vignette.
