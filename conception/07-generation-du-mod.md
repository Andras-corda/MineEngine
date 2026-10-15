# 07. Génération du mod

La génération transforme un projet en projet Minecraft complet, prêt à être compilé par Gradle. Elle se fait en deux temps : le projet devient une **représentation intermédiaire** (Mod IR), puis des **émetteurs** écrivent les fichiers à partir de cette seule IR.

Cette séparation a une raison : le générateur ne doit rien savoir des éléments, de leurs fichiers ni de l'éditeur. Il reçoit une IR déjà validée, dont toutes les références sont résolues en identifiants du jeu. Ajouter un loader ou une version de Minecraft ne touche donc jamais au modèle du projet.

## 1. Construction du Mod IR

1. **Valider le projet entier** ([04](04-modele-du-domaine.md), section 10). Une seule erreur arrête la construction ; les avertissements passent.
2. **Abaisser chaque élément**, dans l'ordre de leurs identifiants en jeu, par l'abaissement de sa sorte. Un abaissement peut encore ajouter des erreurs (référence qui ne se résout pas, fichier de son disparu).
3. S'il n'y a toujours pas d'erreur, l'IR est le couple **(informations du mod, contenu)**.

### Informations du mod

Identifiant, nom, version, auteurs, description, licence, site web, version de Minecraft, identifiant du loader : recopiés depuis les paramètres du projet.

### Résolution des références

- **Référence à un item ou à un bloc** : un élément du projet devient `<modid>:<identifiant en jeu>` ; un identifiant du jeu reste tel quel ; un tag est refusé (erreur « X : un tag n'est pas accepté ici »), sauf pour un ingrédient de recette. Un élément visé supprimé donne une erreur qui cite le rôle du lien (« Item laissé : l'asset visé a été supprimé »).
- **Référence à un son** : même règle (un tag est ignoré, la validation l'a déjà refusé).
- **Texture** : le chemin absolu de l'image. Sans texture, la texture de remplacement. Avec une texture dont le fichier a disparu : la texture de remplacement et un avertissement.

### Ce que devient chaque sorte d'élément

**Item** : toutes ses propriétés, avec ces ajustements :

- taille de pile forcée à 1 si l'item a une durabilité ou s'il est un équipement ;
- durabilité ramenée à 0 pour un équipement (elle vient du matériau) ;
- durée des effets de nourriture convertie en ticks (× 20) ;
- item rendu après fabrication résolu.

**Bloc** : ses réglages, ses textures d'emplacement résolues, sa forme item (s'il en a une), et ce qu'il laisse tomber :

- niveau d'outil ramené à « tous » s'il n'y a pas d'outil ;
- expérience mise à 0 pour une colonne ; minimum et maximum remis dans l'ordre ;
- `Self` sans forme item devient `Nothing` ; `OtherItem` dont l'item ne se résout pas devient `Nothing` ; quantités remises dans l'ordre.

**Mob** : ses propriétés, avec :

- taille effective (personnalisée, ou celle du modèle) ;
- comportements réservés aux animaux retirés d'un mob qui n'est pas un animal ; comportements dont l'item ne se résout pas retirés ;
- item de reproduction gardé seulement pour un animal ;
- butin aux quantités remises dans l'ordre ;
- œuf d'apparition (`<id>_spawn_egg`, deux couleurs) s'il est activé ;
- apparition naturelle seulement si elle est activée et qu'au moins un biome est choisi (biomes triés, groupes remis dans l'ordre) ;
- texture seulement si elle existe (sinon le jeu utilise celle de la créature d'origine).

**Recette** :

- **avec disposition** : la grille est réduite au plus petit rectangle qui contient les cases remplies (le jeu accepte alors le motif n'importe où). Chaque ingrédient distinct reçoit une lettre (`A` à `I`, dans l'ordre de lecture de la grille), et le motif s'écrit ligne par ligne, avec une espace pour une case vide ;
- **sans disposition** : la liste des ingrédients des cases remplies ;
- **four** : l'ingrédient, l'expérience, la durée de cuisson.

Un ingrédient peut être un tag. Une recette dont le résultat ou un ingrédient ne se résout pas est ignorée (l'erreur est déjà signalée).

**Texture** : rien (les textures sont copiées par les éléments qui les utilisent). **Son** : son fichier et son sous-titre ; un fichier disparu est une erreur.

**Graphe** : voir la section suivante.

## 2. Compilation des graphes

Le compilateur de graphes suit le fil d'exécution et produit des instructions. Les règles sont celles de la sémantique décrite dans [05](05-programmation-nodale.md), section 8.

### Ce que devient chaque sorte de graphe

| Graphe | IR |
| --- | --- |
| Événements | un **comportement** : la liste des réactions (une par événement qui déclenche au moins une instruction) et les variables du graphe avec leur valeur initiale. Un graphe sans réaction n'est pas généré. |
| Fonction, procédure | une **fonction** : paramètres, valeurs renvoyées (aucune pour une procédure), variables locales et globales avec leur valeur initiale, drapeaux statique et pure, corps compilé depuis l'entrée |
| Classe | une **classe** : parente, interfaces, champs avec leur valeur initiale, paramètres du constructeur, corps du constructeur (commençant par l'appel du constructeur parent s'il y en a un) |
| Structure | une **structure** : ses champs |
| Interface | une **interface** : ses signatures |
| Énumération | une **énumération** : ses constantes |

Chaque définition garde son identifiant interne (pour rattacher les erreurs Java), son identifiant en jeu, son nom, son accessibilité et son propriétaire.

Une réaction à un **événement d'item** porte en plus l'item du mod visé : son code sera appelé depuis la classe Java de cet item. Un événement d'item qui vise un bloc du projet est une erreur.

### Instructions

| Instruction | Produite par |
| --- | --- |
| Action (sorte, arguments dans l'ordre des entrées) | nœuds d'action du catalogue |
| Si / sinon (condition, branche vraie, branche fausse) | nœuds condition ; « Si » prend son entrée, les autres conditions deviennent un appel de calcul booléen |
| Appel (fonction ou méthode d'interface, objet ou « Moi », arguments, variables locales qui reçoivent les résultats) | appel non pur |
| Création d'objet (type, arguments, variable locale) | « Créer » |
| Affectation de variable, de champ | « Modifier » |
| Retour (valeurs) | « Retour » |
| Appel du constructeur parent (arguments) | « Constructeur parent » |

### Expressions

Littéral (nombre, texte, booléen, item / bloc / son résolu, constante d'énumération), valeur de l'événement (`player`, `position`), paramètre, variable locale (`_vN`), variable du graphe, champ (d'un objet ou de « Moi »), « Moi », calcul d'un nœud du catalogue (arguments dans l'ordre des entrées), appel d'une fonction pure (avec, si elle renvoie plusieurs valeurs, laquelle est voulue), conversion d'un nombre en texte, valeur par défaut d'un type.

Les locales d'une réaction ou d'un corps sont déclarées en tête, dans l'ordre de leur première utilisation, nommées `_v1`, `_v2`...

## 3. Le générateur

### Déroulement

1. **Vérifier la cible** : le loader et la version de Minecraft du projet doivent être ceux du MDK choisi. Sinon, une erreur explique de choisir un MDK correspondant.
2. **Lire la liste des fichiers générés la fois précédente** (`.mineengine/generated.json`).
3. **Préparer le workspace** à partir du MDK (backend, section 5).
4. **Exécuter les émetteurs communs**, puis ceux du backend. Chacun annonce son nom dans le journal (niveau debug).
5. **Supprimer les fichiers générés la fois précédente qui ne l'ont pas été cette fois** (élément supprimé ou renommé), sauf s'ils sont verrouillés, puis les dossiers devenus vides (en remontant, sans dépasser la racine du projet).
6. **Enregistrer la nouvelle liste** (fichiers écrits et fichiers verrouillés conservés).

Le résultat de la génération est la liste des fichiers écrits, les diagnostics et la carte des sources.

### Règles d'écriture

- **Jamais hors du projet** : un chemin qui sort du dossier du projet est refusé.
- **Texte** : UTF-8 sans BOM, fins de ligne LF.
- **Un fichier identique n'est pas réécrit**, pour que Gradle et l'éditeur ne le voient pas changer.
- **Un fichier verrouillé qui existe n'est pas réécrit**, mais il reste dans la liste des fichiers générés.
- **Jamais de suppression d'un fichier que Mine Engine n'a pas généré** : le code écrit à la main dans `src/main/java` ne figure pas dans la liste, il n'est donc jamais touché.

### Émetteurs communs à tous les loaders

| Émetteur | Fichiers |
| --- | --- |
| Ressources des items | modèle et texture de chaque item |
| Ressources des blocs | textures, état, modèle de bloc, modèle d'item (si forme item), table de butin |
| Ressources des mobs | texture (si fournie), modèle de l'œuf, table de butin (si butin) |
| Recettes | un fichier par recette |
| Sons | `sounds.json` et copie des OGG |
| Fichiers de langue | `en_us.json` et `fr_fr.json` |
| Tags | tags d'outils des blocs, tags d'items |

### Carte des sources

Chaque émetteur de code Java note quelles lignes de quel fichier proviennent de quel élément (et, pour un graphe, de quel nœud). Quand plusieurs plages contiennent une ligne, **la plus petite l'emporte** : une erreur dans une méthode est rattachée à la méthode, pas à sa classe. Voir [08](08-mdk-jdk-et-compilation.md) pour l'usage qui en est fait.

## 4. Ressources Minecraft produites

Toutes sous `src/main/resources/`. Dans ce qui suit, `<ns>` est l'identifiant du mod. **À partir de Minecraft 1.21**, les dossiers de données sont au singulier : `loot_table`, `recipe`, `tags/block`, `tags/item` (au lieu de `loot_tables`, `recipes`, `tags/blocks`, `tags/items`).

### Items

- `assets/<ns>/models/item/<id>.json` : parent `minecraft:item/handheld` si l'item est tenu comme un outil, sinon `minecraft:item/generated` ; texture `layer0` = `<ns>:item/<id>`.
- `assets/<ns>/textures/item/<id>.png` : la texture copiée, ou la texture de remplacement.

### Blocs

- Textures : `assets/<ns>/textures/block/<id>.png` pour la principale, `<id>_<emplacement>.png` pour chaque emplacement renseigné et utilisé par la forme.
- État `assets/<ns>/blockstates/<id>.json` : une variante unique `""` vers le modèle ; pour une colonne, trois variantes `axis=x` (rotation x 90, y 90), `axis=y`, `axis=z` (rotation x 90).
- Modèle `assets/<ns>/models/block/<id>.json` :

| Forme | Parent | Textures |
| --- | --- | --- |
| `Cube` | `minecraft:block/cube_all` | `all` |
| `CubeFaces` | `minecraft:block/cube` | `particle` (principale), `up`, `down`, `north`, `south`, `east`, `west` |
| `Column` | `minecraft:block/cube_column` | `end` (emplacement Top), `side` (principale) |
| `Cross` | `minecraft:block/cross` | `cross` |

  Un emplacement vide reprend la texture principale. La transparence est écrite dans le modèle : `render_type` vaut `minecraft:cutout`, `minecraft:cutout_mipped` ou `minecraft:translucent` (rien pour `Solid`).

- Modèle d'item du bloc (si forme item) : parent = le modèle du bloc ; pour une croix, `minecraft:item/generated` avec `layer0` = la texture du bloc.
- Table de butin `data/<ns>/loot_tables/blocks/<id>.json` (sauf `Nothing`) : type `minecraft:block`, un tirage, une entrée item (le bloc lui-même ou l'item choisi), condition `minecraft:survives_explosion`, et pour un autre item une fonction `minecraft:set_count` (nombre fixe, ou `minecraft:uniform` entre le minimum et le maximum).

### Mobs

- `assets/<ns>/textures/entity/<id>.png` si une texture est fournie.
- `assets/<ns>/models/item/<id>_spawn_egg.json` : parent `minecraft:item/template_spawn_egg`.
- `data/<ns>/loot_tables/entities/<id>.json` si le mob a du butin : type `minecraft:entity`, un tirage par item, avec `set_count` uniforme si les quantités ne valent pas 1 et 1.
- Modificateur de biome `data/<ns>/<loader>/biome_modifier/spawn_<id>_<biome>.json` (un par biome) : type `<loader>:add_spawns`, `biomes` (identifiant ou tag du tableau ci-dessous), `spawners` (type = le mob, poids, `minCount`, `maxCount`). `<loader>` vaut `forge` ou `neoforge`.

| Biome | Cible | Biome | Cible |
| --- | --- | --- | --- |
| Overworld | `#minecraft:is_overworld` | Mountain | `#minecraft:is_mountain` |
| Plains | `minecraft:plains` | Badlands | `#minecraft:is_badlands` |
| Forest | `#minecraft:is_forest` | Beach | `#minecraft:is_beach` |
| Taiga | `#minecraft:is_taiga` | Ocean | `#minecraft:is_ocean` |
| Jungle | `#minecraft:is_jungle` | River | `#minecraft:is_river` |
| Savanna | `#minecraft:is_savanna` | Nether | `#minecraft:is_nether` |
| Desert | `minecraft:desert` | End | `#minecraft:is_end` |
| Swamp | `minecraft:swamp` | | |

### Recettes

`data/<ns>/recipes/<id>.json`, catégorie `misc` :

- **avec disposition** : type `minecraft:crafting_shaped`, `pattern`, `key` (lettre vers `{ "item": ... }` ou `{ "tag": ... }`), `result` ;
- **sans disposition** : type `minecraft:crafting_shapeless`, `ingredients`, `result` ;
- **four** : type `minecraft:smelting`, `ingredient`, `result`, `experience`, `cookingtime`.

Le résultat s'écrit `{ "item": ..., "count": n }` avant 1.20.5 et `{ "id": ..., "count": n }` à partir de 1.20.5 ; pour le four, l'identifiant seul avant 1.20.5, `{ "id": ... }` ensuite.

### Sons

- `assets/<ns>/sounds/<id>.ogg` : le fichier copié.
- `assets/<ns>/sounds.json` : pour chaque son, `{ "sounds": [ { "name": "<ns>:<id>" } ], "subtitle": "subtitles.<ns>.<id>" }` (le sous-titre seulement s'il existe). Rien n'est écrit si le mod n'a aucun son.

### Langues

`assets/<ns>/lang/en_us.json` et `fr_fr.json`, avec les mêmes noms (ceux saisis par l'utilisateur, il n'y a pas de traduction des contenus) :

| Clé | Valeur |
| --- | --- |
| `itemGroup.<ns>` | nom du mod (onglet créatif) |
| `item.<ns>.<id>` | nom de chaque item |
| `block.<ns>.<id>` | nom de chaque bloc |
| `tooltip.<ns>.<id>.<n>` | chaque ligne d'info-bulle (n à partir de 0) |
| `entity.<ns>.<id>` | nom de chaque mob |
| `subtitles.<ns>.<id>` | sous-titre de chaque son |
| `item.<ns>.<id>_spawn_egg` | « Œuf d'apparition de X » en français, « X Spawn Egg » en anglais |

### Tags

- Blocs avec un outil : `data/minecraft/tags/blocks/mineable/<outil>.json` (pickaxe, axe, shovel, hoe), et pour un niveau d'outil `needs_<stone|iron|diamond>_tool.json`.
- Tags d'items choisis par l'utilisateur : `data/<espace>/tags/items/<chemin>.json`.
- À partir de 1.20.5, les items de viande sont ajoutés à `data/minecraft/tags/items/meat.json`.
- Format : `{ "replace": false, "values": ["<ns>:<id>", ...] }`, valeurs triées. Un tag alimenté par plusieurs sources n'est écrit qu'une fois.

### Texture de remplacement

Un PNG de 16 × 16 pixels, damier de carrés de 8 × 8 : magenta (248, 0, 248) en haut à gauche et en bas à droite, noir ailleurs. Il est encodé par Mine Engine lui-même (pas de dépendance à une bibliothèque d'images).

## 5. Les backends

Un backend sait trois choses : préparer le workspace à partir de son MDK, fournir ses émetteurs propres, et donner les tâches Gradle (`build` pour le .jar, `runClient` pour lancer le jeu) et le chemin du .jar produit (`build/libs/<modid>-<version>.jar`).

### Préparation du workspace (commune)

1. Le dossier du MDK doit exister.
2. **Changement de MDK** : si `.mineengine/mdk` désigne un autre MDK que celui d'aujourd'hui, les dossiers `build/` et `.gradle/` sont vidés (pour ne pas mélanger deux versions). Les fichiers de l'ancien MDK absents du nouveau disparaîtront avec les autres fichiers générés devenus inutiles.
3. **Copie du projet Gradle du MDK**, sauf : `src/main/java`, `src/main/resources`, `.github`, `.gradle`, `build`, `run`, `.idea`, `README.md`, `README.txt`, `TEMPLATE_LICENSE.txt`, `LICENSE.txt`, `CREDITS.txt`, `changelog.txt`, `.gitignore`, `.gitattributes`, `mdk.json`, `gradle.properties`, et les exclusions propres au backend. Ces fichiers sont des fichiers générés : verrouillés, ils ne sont plus réécrits (pour ajouter une dépendance dans `build.gradle`, par exemple).
4. **`gradle.properties`** est relu depuis le MDK, ses commentaires conservés, puis : `mod_id`, `mod_name`, `mod_license` (`All Rights Reserved` si vide), `mod_version`, `mod_group_id` (le package racine Java). Les guillemets doubles du nom et de la licence deviennent des simples. L'argument `-Dfile.encoding=UTF-8` est ajouté à `org.gradle.jvmargs` (sans lui, Gradle lancé avec Java 17 sous Windows écrit `mods.toml` en Windows-1252 et corrompt les accents). Les caractères non ASCII sont écrits `\uXXXX`.
5. Réglages propres au backend.
6. Écriture de `.mineengine/mdk`.

### Forge (1.20.1 et 1.21.1)

- `gradle.properties` : `mod_authors` et `mod_description` (« Mod créé avec Mine Engine. » si vide).
- Émetteur des métadonnées : recopie `src/main/resources/META-INF/mods.toml` du MDK (avec `displayURL` = site web, s'il y en a un) et `pack.mcmeta`.
- Émetteurs : code Java Forge, métadonnées, modificateurs de biome `forge`.

### NeoForge (1.21.1)

- Le modèle `src/main/templates/META-INF/neoforge.mods.toml` est exclu de la copie, puis réécrit : description d'exemple remplacée, `authors` et `displayURL` renseignés s'ils existent.
- Émetteurs : code Java NeoForge, modificateurs de biome `neoforge`, combustibles par data map (`data/neoforge/data_maps/item/furnace_fuels.json` : `{ "values": { "<ns>:<id>": { "burn_time": n } } }`).

## 6. Code Java produit

### Organisation

- Package racine : `com.mineengine.mods.<modid>` (l'identifiant du mod, nettoyé pour être un segment de package valide).
- Classe principale (package racine) : le nom du mod en PascalCase, suffixé de `Mod` s'il ne l'est pas déjà (`MonModMod`... ou `RubyMod`). Elle contient `MOD_ID`, et son constructeur inscrit tous les registres sur le bus du mod, les onglets créatifs, les attributs et règles d'apparition des mobs, les comportements des graphes sur le bus du jeu, et (côté client) les rendus des mobs.
- Package `registry` : `ModBlocks`, `ModItems`, `ModCreativeTabs`, `ModSounds` (si le mod a des sons), `ModEntities` (s'il a des mobs).
- Package `entity` : une classe par mob, `<Id>Entity`.
- Package `client` : `ModEntityRenderers`.
- Package `behavior` : une classe par graphe d'événements, `<Id>Behavior`.
- Package `code` : une classe par définition hors de toute classe (section 7).

Chaque élément est enregistré par un champ `public static final` dont le nom est l'identifiant en majuscules (`MAGIC_SWORD`) ; un nom qui entre en collision avec un champ du code généré (`BLOCKS`, `ITEMS`, `CREATIVE_MODE_TABS`, `MAIN_TAB`, `MOD_ID`, `ENTITY_TYPES`, `SOUND_EVENTS`) reçoit le suffixe `_ENTRY`. Un élément qui a besoin d'un comportement particulier est créé par une sous-classe anonyme.

En tête de chaque fichier : un commentaire qui dit que le fichier est généré par Mine Engine et réécrit à chaque build.

### Contenu

- **Blocs** : `Block` (ou bloc à expérience) avec ses propriétés (`strength`, ou `strength(-1, 3600000)` pour un bloc incassable ; `sound` ; `lightLevel` ; `requiresCorrectToolForDrops` ; `noOcclusion` pour un bloc transparent ou en croix ; `noCollission` pour une croix ; `friction`, `speedFactor`, `jumpFactor` s'ils diffèrent des valeurs normales). Une colonne est un bloc orientable.
- **Items** : `Item`, ou la classe d'outil, d'arme ou d'armure de Minecraft avec son matériau ; propriétés (pile, rareté, résistance au feu, durabilité, nourriture). Redéfinitions si nécessaire : effet brillant, info-bulle (une ligne traduite par ligne), item rendu après fabrication, non réparable, durée de combustion (Forge), événements d'item (appel des méthodes du comportement). La forme item des blocs est un `BlockItem`.
- **Onglets créatifs** : un onglet propre au mod (titre `itemGroup.<ns>`, icône le premier élément), et l'ajout aux onglets vanilla choisis.
- **Sons** : un `SoundEvent` par son.
- **Mobs** : type d'entité (famille `MONSTER` ou `CREATURE`, taille), classe d'entité qui hérite de `Monster`, `PathfinderMob` ou `Animal`, attributs, comportements d'IA dans l'ordre, sons, comportement au soleil, persistance ; règles d'apparition au sol selon la famille ; œuf d'apparition ; rendu par le modèle et la texture de la créature d'origine (ou celle du mod).
- **Comportements** : une classe par graphe d'événements, avec ses variables en champs statiques, une méthode par réaction, les petites méthodes utilitaires dont le code a besoin (donner un item, jouer un son...), et une méthode `register()` qui inscrit les réactions sur le bus du jeu. Chaque réaction commence par un filtrage (côté serveur seulement, main principale pour les clics) et définit `player`, `level` et `position`.

Correspondance des comportements d'IA :

| Comportement | Classe Minecraft |
| --- | --- |
| Swim | `FloatGoal` |
| Panic | `PanicGoal(vitesse)` |
| MeleeAttack | `MeleeAttackGoal(vitesse, option)` |
| LeapAtTarget | `LeapAtTargetGoal(hauteur)` |
| RandomStroll | `WaterAvoidingRandomStrollGoal(vitesse)` |
| LookAtPlayer | `LookAtPlayerGoal(Player, distance)` |
| RandomLookAround | `RandomLookAroundGoal` |
| Tempt | `TemptGoal(vitesse, ingrédient = l'item, false)` |
| Breed | `BreedGoal(vitesse)` |
| FollowParent | `FollowParentGoal(vitesse)` |
| AvoidEntity | `AvoidEntityGoal(cible, distance, vitesse, vitesse × 1,25)` |
| FleeSun | `FleeSunGoal(vitesse)` |
| MoveTowardsTarget | `MoveTowardsTargetGoal(vitesse, distance)` |
| HurtByTarget | `HurtByTargetGoal`, avec `setAlertOthers()` si l'option est cochée |
| AttackNearest | `NearestAttackableTargetGoal(cible, option)` |

Cibles : `Player`, `AbstractVillager`, `IronGolem`, `Animal`, `Monster`.

### Différences entre loaders

| Sujet | Forge | NeoForge |
| --- | --- | --- |
| Constructeur de la classe principale | reçoit le contexte de chargement, en tire le bus du mod | reçoit le bus du mod |
| Registres | `DeferredRegister.create(ForgeRegistries...)` et `RegistryObject<T>` | `DeferredRegister.Blocks`, `.Items` et `DeferredBlock<T>`, `DeferredItem<T>` |
| Bus du jeu | `MinecraftForge.EVENT_BUS` | `NeoForge.EVENT_BUS` |
| Événements de jeu | `PlayerInteractEvent.RightClickBlock`, `.RightClickItem`, `PlayerEvent.PlayerLoggedInEvent`, `BlockEvent.BreakEvent` | leurs équivalents NeoForge |
| Combustible | redéfinition de `getBurnTime` dans l'item | data map `furnace_fuels` |

### Différences entre versions de Minecraft

| Sujet | 1.20 à 1.20.4 | 1.20.5 et plus (dont 1.21.1) |
| --- | --- | --- |
| Viande | dans les propriétés de nourriture | tag `minecraft:meat` |
| Info-bulle | `appendHoverText(stack, level, tooltip, flag)` | `appendHoverText(stack, context, tooltip, flag)` |
| Identifiant de ressource | `new ResourceLocation(texte)` | `ResourceLocation.parse(texte)` |
| Mettre le feu à soi-même | `setSecondsOnFire(n)` | `igniteForSeconds(n)` |
| Résultat de recette | clé `item` | clé `id` |
| Dossiers de données | pluriels (jusqu'à 1.20.6) | singuliers à partir de 1.21 |

Ces différences sont regroupées dans un seul objet « API du jeu » choisi d'après la version, pour que le reste de l'émetteur reste commun.

### Formatage

Le code produit est indenté de 4 espaces, avec des accolades en fin de ligne. Une chaîne d'appels de propriétés est écrite sur une ligne tant qu'elle est courte, sinon un appel par ligne. Le même formateur sert à la commande « Formater » de l'éditeur de code.

## 7. Code Java des définitions

- **Package `code`**. Une définition hors de toute classe devient un fichier : une fonction devient une classe qui porte une méthode statique du même nom ; une classe, une structure (une classe `final` à champs publics, construite avec la valeur de chacun), une interface ou une énumération devient le type Java correspondant. Une définition intégrée à une classe en devient un membre (méthode, type imbriqué `Parente.Imbriquee`).
- **Le monde en premier paramètre** : toute fonction, procédure, méthode et tout constructeur reçoit `Level level` en premier paramètre, pour que les nœuds qui agissent sur le jeu fonctionnent partout (comme le « World Context » d'Unreal). Les appels le transmettent.
- **Plusieurs valeurs renvoyées** : un `record` qui les porte, déclaré par la fonction (sauf si elle redéfinit une méthode parente, qui le déclare déjà).
- **Héritage** : `extends`, `implements`, `@Override` sur les redéfinitions et les implémentations d'interface.
- **Variables globales** d'une fonction : champs statiques de sa classe.
- **Noms** : tirés des noms saisis (voir [05](05-programmation-nodale.md)), en évitant les mots-clés Java et les noms réservés du code généré (`level`, `player`, `position`, `event`, `result`, `random`).
- **Imports** : seulement ceux dont le code a besoin, triés.

## 8. Ce que la génération ne fait pas (encore)

- Pas de traduction des textes du mod en d'autres langues que celles saisies.
- La description par défaut de `mods.toml` et le nom des œufs d'apparition sont en dur (en français pour la description).
- Pas de génération pour Fabric ni pour d'autres versions que 1.20.1 et 1.21.1.
