# 06. Formats de fichiers

Ce document décrit tout ce que Mine Engine lit ou écrit sur le disque. **Une réécriture doit lire et écrire ces formats à l'identique** : les projets existants doivent s'ouvrir sans conversion, et un projet enregistré deux fois de suite ne doit pas changer d'un octet (pour que les diffs Git restent propres).

## 1. Conventions communes

Tous les fichiers JSON écrits par Mine Engine suivent les mêmes règles :

- **UTF-8 sans BOM**, fins de ligne **LF**, indentation de **2 espaces**, une fin de ligne après l'accolade finale.
- **Caractères non ASCII écrits tels quels** (pas d'échappement `\u00e9`) ; seuls les caractères que JSON impose sont échappés.
- **Ordre des clés fixe** : celui de ce document. Les ensembles qui n'ont pas d'ordre naturel sont triés : onglets créatifs et biomes dans l'ordre de déclaration de leur énumération (celui des tableaux de [04](04-modele-du-domaine.md)), valeurs d'un nœud par nom de broche.
- **Écriture atomique** : le contenu est écrit dans `<fichier>.tmp`, puis ce fichier remplace la cible en une seule opération. Un arrêt brutal laisse soit l'ancien fichier, soit le nouveau, jamais un fichier tronqué.
- **Lecture tolérante** : une propriété absente prend sa valeur par défaut ; une valeur d'énumération inconnue prend la valeur par défaut ; un élément de liste illisible est ignoré. En revanche, une valeur du mauvais type JSON (un texte là où un nombre est attendu) est une erreur qui cite le fichier et la propriété.
- **Énumérations** écrites avec leur nom anglais (`"Common"`, `"Pickaxe"`), lues sans tenir compte de la casse.
- **Identifiants internes** écrits au format UUID avec tirets, en minuscules (`3f2b9c1e-...`).
- **Chemins** relatifs au projet, avec des `/`.

## 2. Organisation d'un projet

Depuis la V0.4.2, le dossier du projet est aussi le projet Gradle du mod (comme un workspace MCreator).

```
Mon mod/
  Mon mod.mineengine          paramètres du projet
  .gitignore                  build/ run/ .gradle/ .mineengine/
  elements/                   un fichier par élément, rangé par sorte
    items/      ruby.asset.json
    blocks/     ruby_ore.asset.json
    mobs/       goblin.asset.json
    recipes/    ruby_from_ore.asset.json
    textures/   ruby.asset.json
    sounds/     goblin_roar.asset.json
    graphs/     magic_wand.asset.json
  Textures/                   images PNG importées
  Sounds/                     sons OGG importés
  src/main/java/              code Java : généré et écrit à la main
  src/main/resources/         ressources générées (modèles, langues, recettes...)
  src/main/templates/         (NeoForge) modèle de neoforge.mods.toml
  build.gradle, settings.gradle, gradlew, gradlew.bat, gradle/...   recopiés depuis le MDK
  gradle.properties           recopié depuis le MDK puis adapté au mod
  build/libs/                 mods .jar produits par Gradle
  run/                        dossier de jeu (mondes de test)
  .gradle/                    cache de Gradle
  .mineengine/                cache local de Mine Engine
    generated.json            liste des fichiers générés
    mdk                       identifiant du MDK de la dernière génération
    backups/                  sauvegardes de secours
```

- **Nom du fichier projet** : le nom du dossier, nettoyé des caractères interdits par Windows, suivi de `.mineengine` (« Projet.mineengine » si rien ne reste). À l'ouverture d'un dossier, le premier fichier `*.mineengine` (ordre alphabétique) est utilisé.
- **Nom du dossier d'un nouveau projet** : le nom du mod nettoyé de la même façon (l'identifiant du mod si rien ne reste), créé dans le dossier parent choisi. Le dossier ne doit pas exister ou doit être vide.
- **Fichier d'un élément** : `elements/<sous-dossier>/<identifiant en jeu>.asset.json`. Le sous-dossier dépend de la sorte : `items`, `blocks`, `mobs`, `recipes`, `textures`, `sounds`, `graphs`.
- **À l'enregistrement**, tous les éléments sont écrits, puis tout fichier `*.asset.json` de `elements/` qui n'a pas été écrit est supprimé (élément renommé ou supprimé).
- **À l'ouverture**, tous les fichiers `*.asset.json` de `elements/` (sous-dossiers compris, dans l'ordre des chemins) sont lus. Leur emplacement ne compte pas : c'est la propriété `type` qui fait foi.

## 3. Fichier projet (`.mineengine`)

```json
{
  "formatVersion": 1,
  "modId": "monmod",
  "modName": "Mon mod",
  "modVersion": "1.0.0",
  "authors": "",
  "description": "",
  "license": "All Rights Reserved",
  "website": "",
  "mdk": "forge-1.20.1-47.4.10",
  "minecraftVersion": "1.20.1",
  "loader": "forge",
  "folders": ["Armes", "Armes/Épées"],
  "lockedFiles": ["src/main/java/com/mineengine/mods/monmod/registry/ModItems.java"]
}
```

- `formatVersion` supérieur à 1 : refus d'ouvrir (« créé par une version plus récente »).
- `modId` est obligatoire et doit être valide. `mdk` peut être `null`.

## 4. Fichier d'un élément (`.asset.json`)

### Enveloppe commune

```json
{
  "formatVersion": 2,
  "guid": "3f2b9c1e-4d5a-4b6c-8e7f-0a1b2c3d4e5f",
  "type": "Item",
  "resourceId": "ruby",
  "displayName": "Rubis",
  "folder": "Ressources",
  "properties": { }
}
```

- `formatVersion` : 2. Supérieur : refus. Inférieur : le fichier est converti en mémoire à l'ouverture (section 9), puis réécrit au format courant au prochain enregistrement.
- `guid`, `type`, `resourceId` sont obligatoires et doivent être valides.
- `folder` n'est écrit que si l'élément est rangé dans un dossier.
- `properties` dépend de la sorte (ci-dessous). Les références de contenu y sont écrites sous leur forme texte : `asset:<uuid>`, `espace:chemin`, `#espace:tag`, ou `null`.

### Propriétés communes aux items et aux blocs

```json
"texture": "uuid ou null",
"maxStackSize": 64,
"rarity": "Common",
"fireResistant": false,
"tooltip": ["Ligne 1", "Ligne 2"],
"inModCreativeTab": true,
"creativeTabs": ["Combat", "Ingredients"]
```

### Item

Après les propriétés communes :

```json
"durability": 0,
"glint": false,
"food": {
  "nutrition": 4,
  "saturation": 0.3,
  "alwaysEdible": false,
  "meat": false,
  "fast": false,
  "effects": [ { "effect": "Regeneration", "seconds": 5, "amplifier": 1, "probability": 1 } ]
},
"equipment": {
  "kind": "Simple",
  "tier": "Iron",
  "armorMaterial": "Iron",
  "attackDamage": 3,
  "attackSpeed": -2.4
},
"handheld": false,
"craftRemainder": null,
"noRepair": false,
"burnTime": 0,
"tags": ["c:gems/ruby"]
```

`food` vaut `null` pour un item non comestible : sa présence décide si l'item est comestible.

### Bloc

Après les propriétés communes :

```json
"model": "Cube",
"renderType": "Solid",
"lightLevel": 0,
"textures": { "top": "uuid", "bottom": "uuid" },
"hardness": 1.5,
"resistance": 6,
"unbreakable": false,
"sound": "Stone",
"friction": 0.6,
"speedFactor": 1,
"jumpFactor": 1,
"harvestTool": "Pickaxe",
"toolTier": "Iron",
"requiresCorrectTool": true,
"drop": { "kind": "OtherItem", "item": "asset:uuid", "min": 1, "max": 3 },
"experience": { "min": 3, "max": 7 },
"hasItem": true
```

`textures` ne contient que les emplacements renseignés (clés en minuscules : `top`, `bottom`, `north`, `south`, `east`, `west`), dans l'ordre de cette liste.

### Mob

```json
"texture": "uuid ou null",
"model": "Zombie",
"kind": "Monster",
"hitbox": { "custom": false, "width": 0.6, "height": 1.8 },
"attributes": {
  "health": 20, "armor": 0, "movementSpeed": 0.25,
  "attackDamage": 3, "followRange": 16, "knockbackResistance": 0
},
"experience": 5,
"fireImmune": false,
"burnsInDaylight": false,
"persistent": false,
"goals": [
  { "kind": "Swim" },
  { "kind": "MeleeAttack", "speed": 1.2, "flag": false },
  { "kind": "AttackNearest", "target": "Player", "flag": true }
],
"breedingItem": null,
"sounds": { "ambient": null, "hurt": "asset:uuid", "death": "minecraft:entity.zombie.death" },
"drops": [ { "item": "minecraft:rotten_flesh", "min": 0, "max": 2 } ],
"spawnEgg": { "enabled": true, "primaryColor": 5011498, "secondaryColor": 2043412 },
"spawning": { "enabled": false, "biomes": ["Overworld"], "weight": 10, "minGroup": 1, "maxGroup": 3 }
```

Un comportement (`goals`) n'écrit que les réglages que sa sorte utilise (`speed`, `distance`, `target`, `item`, `flag`), dans cet ordre (voir [04](04-modele-du-domaine.md)). À la lecture, les réglages absents prennent les valeurs par défaut de la sorte. Les couleurs de l'œuf sont des entiers RGB.

### Recette

```json
"kind": "Shaped",
"grid": ["asset:uuid", null, null, "#minecraft:planks", null, null, null, null, null],
"input": null,
"result": "minecraft:diamond",
"count": 1,
"experience": 0.1,
"cookingTime": 200
```

`grid` a toujours 9 cases, ligne par ligne. À la lecture, une grille plus courte est complétée par des cases vides.

### Texture et son

```json
"file": "Textures/ruby-1a2b3c4d.png"
```

```json
"file": "Sounds/goblin_roar-5e6f7a8b.ogg",
"subtitle": "Le gobelin rugit"
```

### Graphe

Pour un graphe d'événements, `kind` et `access` ne sont pas écrits (le fichier reste celui des versions antérieures à la 0.4.3). Toutes les propriétés vides ou fausses sont omises.

```json
"kind": "Class",
"access": "Public",
"owner": "uuid de la classe propriétaire",
"static": true,
"pure": true,
"parameters": [ { "id": "uuid", "name": "Points de vie", "type": "number", "default": "20" } ],
"returns": [ ],
"variables": [ { "id": "uuid", "name": "Cible", "type": "object:uuid", "access": "Private", "global": true } ],
"baseClass": "uuid",
"interfaces": ["uuid"],
"constants": [ { "id": "uuid", "name": "Très rare" } ],
"methods": [ { "id": "uuid", "name": "Attaquer", "parameters": [ ], "returns": [ ] } ],
"nodes": [
  {
    "id": "uuid", "kind": "CallFunction", "x": 420, "y": 120,
    "target": "uuid", "member": "uuid",
    "values": { "count": "3", "item": "minecraft:diamond" }
  }
],
"connections": [
  { "from": "uuid", "fromPin": "then", "to": "uuid", "toPin": "exec" }
]
```

- Une variable n'écrit `default` que s'il n'est pas vide, `access` que s'il n'est pas `Public`, `global` que s'il est vrai.
- Les types de variable sont écrits `boolean`, `number`, `text`, `player`, `position`, `item`, `block`, `sound`, `object:<uuid>`, `enum:<uuid>`.
- Les positions des nœuds sont arrondies à un chiffre après la virgule. `target` et `member` ne sont écrits que s'ils existent. Les valeurs d'un nœud sont triées par nom de broche (ordre ordinal).
- À la lecture, un nœud de sorte inconnue (fichier plus récent) est ignoré, avec ses fils ; un fil vers un nœud absent est ignoré.

## 5. Fichiers importés

Une image importée devient un élément Texture, un son un élément Sound :

- **Contrôle du contenu** par signature : un PNG commence par `89 50 4E 47 0D 0A 1A 0A`, un OGG par `OggS`. Sinon : « 'x' n'est pas une image PNG valide ».
- **Copie** dans `Textures/` ou `Sounds/` sous le nom `<identifiant>-<empreinte>.<ext>`, où l'empreinte est formée des 8 premiers caractères hexadécimaux (minuscules) du SHA-256 du fichier. Un nouveau fichier n'écrase donc jamais l'ancien, ce qui permet d'annuler le remplacement de l'image d'une texture.
- **Identifiant** tiré du nom du fichier, rendu unique dans le projet et parmi les autres fichiers du même import. **Nom affiché** : le nom du fichier sans extension.

## 6. Fichiers du cache local (`.mineengine/`)

| Fichier | Contenu |
| --- | --- |
| `generated.json` | `{ "files": ["src/main/...", ...] }` : chemins relatifs des fichiers écrits par la dernière génération (verrouillés compris). Illisible : liste vide. Seule la clé `files` compte ; la mise en forme est libre (cache interne). |
| `mdk` | texte brut : l'identifiant du MDK de la dernière génération |
| `backups/backup-AAAAMMJJ-HHMMSS.zip` | sauvegarde de secours : le fichier projet, `elements/`, `Textures/`, `Sounds/` et `src/`, chemins relatifs au projet |
| `backups/avant-0.4.2-AAAAMMJJ-HHMMSS.zip` | archive d'un projet converti depuis l'ancienne organisation |

Une sauvegarde est d'abord écrite dans `.zip.tmp`, puis renommée. Les sauvegardes sont listées d'après la date de leur nom.

## 7. Fichiers de l'application (`%LOCALAPPDATA%\MineEngine\`)

| Fichier | Contenu |
| --- | --- |
| `settings.json` | préférences : `{ "theme": "System", "language": "en", "autoSaveMinutes": 5, "backupsToKeep": 10 }` (thème `System`, `Light` ou `Dark` ; langue `en` ou `fr` ; sauvegarde automatique 0 à 120 minutes, 0 la désactive ; sauvegardes conservées 1 à 100). Illisible : valeurs par défaut. |
| `projects.json` | projets connus : `{ "projects": [ { "path": "C:\\...", "lastOpened": "2026-10-15T18:22:00.0000000+02:00" } ] }` (`lastOpened` au format ISO 8601 aller-retour, ou `null`). Illisible : liste vide. |
| `layout.v2.xml` | disposition des panneaux de l'éditeur (format propre à la bibliothèque de panneaux : une réécriture peut en choisir un autre, sous un autre nom) |
| `logs/mineengine-AAAAMMJJ.log` | journal du jour : `HH:mm:ss.fff [NIVEAU ] message`, niveau sur 7 caractères (`DEBUG`, `INFO`, `SUCCESS`, `WARNING`, `ERROR`). Les journaux de plus de 14 jours sont supprimés au démarrage. |
| `mdks/<identifiant>/` | un MDK installé (le projet Gradle extrait), avec son `mdk.json` |

`mdk.json`, dans le dossier de chaque MDK :

```json
{
  "id": "forge-1.20.1-47.4.10",
  "loader": "forge",
  "minecraftVersion": "1.20.1",
  "loaderVersion": "47.4.10",
  "javaVersion": 17,
  "gradleVersion": "8.8",
  "source": "Téléchargé depuis https://maven.minecraftforge.net/...",
  "installedAt": "2026-10-01T14:03:12.0000000+02:00"
}
```

L'identifiant d'un MDK est `<loader>-<version de Minecraft>-<version du loader>`, en minuscules, où tout caractère interdit dans un nom de fichier ou blanc devient `_`.

## 8. Fichiers produits dans le projet

Les fichiers générés (code Java, ressources, fichiers Gradle recopiés) sont décrits dans [07](07-generation-du-mod.md). Ils suivent les conventions de la section 1 (UTF-8 sans BOM, LF), à une exception près : `gradle.properties` est lu par Gradle en ISO-8859-1, et ses caractères non ASCII sont donc écrits `\uXXXX`.

## 9. Anciens formats

La réécriture peut choisir de ne pas reprendre ces conversions, en demandant d'ouvrir d'abord le projet avec Mine Engine 0.4.5. Elles sont décrites pour mémoire.

### Projet antérieur à la 0.4.2

Reconnu par un fichier `Project.json` à la racine et aucun `*.mineengine`. À l'ouverture :

1. Archive de `Project.json`, `Content/`, `Textures/`, `Sounds/`, `Scripts/`, `Graphs/` dans `.mineengine/backups/avant-0.4.2-<date>.zip`.
2. `Project.json` devient `<Dossier>.mineengine` (même contenu).
3. `Content/<Sorte>/x.asset.json` devient `elements/<sorte>/x.asset.json`.
4. Les classes Java de `Scripts/` rejoignent `src/main/java/`, dans le dossier de leur package (lu dans la déclaration `package`).
5. `Generated/` est supprimé (il sera régénéré), sauf ses mondes de test, qui passent dans `run/`.

Des remarques expliquant chaque étape sont affichées à l'utilisateur.

### Éléments au format 1 (V0.1 et V0.2)

- Chaque chemin de texture (`"texture": "Textures/ruby.png"`) devient un élément Texture (un seul par image distincte), désigné par son identifiant interne.
- L'item laissé par un bloc, écrit `"ruby"`, devient une référence vers l'item du projet de cet identifiant s'il existe, sinon vers l'item de Minecraft.

Le projet est marqué « modifié » pour que l'utilisateur enregistre le nouveau format.
