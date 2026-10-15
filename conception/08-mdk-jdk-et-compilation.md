# 08. MDK, JDK et compilation

Mine Engine ne compile pas lui-même le Java : il s'appuie sur la chaîne officielle des mods, Gradle et le MDK du loader. Ce document décrit comment il gère les MDK, trouve un Java capable de lancer Gradle, exécute Gradle et ramène ses erreurs vers les éléments du projet.

## 1. La bibliothèque de MDK

### Emplacement et contenu

La bibliothèque est un dossier (`%LOCALAPPDATA%\MineEngine\mdks`). Chaque sous-dossier est un MDK installé, nommé d'après son identifiant (`forge-1.20.1-47.4.10`), et contient le projet Gradle extrait du MDK et un fichier `mdk.json` ([06](06-formats-de-fichiers.md)).

Un MDK installé est décrit par : identifiant, loader, version de Minecraft, version du loader, version de Java utilisée pour compiler, version du Gradle wrapper (si connue), dossier, origine (« Téléchargé depuis... », « Archive déposée : ... », « Dossier importé : ... »), date d'installation. Son nom affiché est « Forge 1.20.1 (47.4.10) ».

### Relecture (« Actualiser »)

Faite au démarrage et à la demande. Pour chaque sous-dossier (sauf ceux dont le nom commence par `.`) :

- s'il contient `mdk.json`, le MDK est chargé ;
- sinon, s'il contient un MDK (à sa racine ou un niveau en dessous), celui-ci est inspecté et enregistré sur place : `mdk.json` est écrit, l'origine est « Dossier déposé : <nom> » ;
- un dossier illisible est ignoré avec un avertissement dans le journal.

Puis chaque archive `.zip` déposée à la racine de la bibliothèque, et pas encore installée, est extraite et installée (puis supprimée). Les MDK sont enfin triés : version de Minecraft décroissante, puis identifiant.

### Reconnaître un MDK

Un dossier est un MDK s'il contient `gradlew.bat` et `gradle.properties`. L'inspection lit :

- **dans `gradle.properties`** (format `clé=valeur` ou `clé:valeur`, lignes commençant par `#` ou `!` ignorées) : le loader et sa version d'après la première clé présente parmi `neo_version` (NeoForge), `forge_version` (Forge), `loader_version` (Fabric), sinon erreur ; la version de Minecraft dans `minecraft_version` (obligatoire) ;
- **dans `build.gradle`** : la version de Java dans `JavaLanguageVersion.of(N)` ; à défaut, la version imposée par Mojang pour cette version du jeu (21 à partir de 1.20.5, 17 à partir de 1.18, 16 pour 1.17, 8 avant) ;
- **dans `gradle/wrapper/gradle-wrapper.properties`** : la version de Gradle dans le nom de la distribution (`gradle-8.8-bin.zip`).

### Installer

- **Depuis une archive** : extraction dans un dossier temporaire, recherche de la racine du MDK (à la racine de l'archive ou un niveau en dessous), puis installation ; le dossier temporaire est supprimé dans tous les cas. Archive illisible ou sans MDK : erreur explicite.
- **Depuis un dossier** : le dossier est copié (l'original n'est jamais modifié), sans ses dossiers de travail : `.gradle`, `build`, `run`, `.idea`, `out`, `bin`, `.vscode`, `.git`.
- **Un MDK déjà installé** (même identifiant) est refusé.
- **Supprimer** un MDK efface son dossier. Seul un MDK rangé dans la bibliothèque peut être supprimé. Les projets qui l'utilisaient devront en choisir un autre.

### Télécharger un MDK officiel

Le catalogue se limite aux versions que le générateur sait produire :

| MDK | Source |
| --- | --- |
| Forge 1.20.1 (47.4.10) | `https://maven.minecraftforge.net/net/minecraftforge/forge/1.20.1-47.4.10/forge-1.20.1-47.4.10-mdk.zip` |
| Forge 1.21.1 (52.1.0) | `https://maven.minecraftforge.net/net/minecraftforge/forge/1.21.1-52.1.0/forge-1.21.1-52.1.0-mdk.zip` |
| NeoForge 1.21.1 (21.1.252) | archive d'un commit précis du dépôt `NeoForgeMDKs/MDK-1.21.1-ModDevGradle` sur GitHub (`7819b902a351b03fe71db00754d103b5a31c4ebf`) |

Le téléchargement est écrit dans un fichier temporaire, installé comme une archive, puis le fichier temporaire est supprimé. Il est annulable. Le client HTTP s'identifie (`MineEngine/<version>`) et accepte jusqu'à 10 minutes par téléchargement.

### MDK pris en charge par le générateur

Un MDK installé peut être utilisé par un projet seulement si un backend le prend en charge :

| Loader | Versions de Minecraft |
| --- | --- |
| Forge | 1.20.1, 1.21.1 |
| NeoForge | 1.21, 1.21.1 |

Les autres MDK apparaissent dans le gestionnaire avec l'état « Non pris en charge par le générateur ».

## 2. Choisir le MDK d'un projet au moment de compiler

1. Le MDK enregistré dans le projet, s'il est installé.
2. Sinon, un MDK installé du même loader et de la même version de Minecraft, que le générateur prend en charge ; un avertissement signale la substitution.
3. Sinon, erreur : « Le MDK 'X' n'est pas installé. Installez-le avec Outils > Gestionnaire de MDK, ou choisissez-en un autre dans Projet > Paramètres du projet. »
4. Un MDK trouvé mais non pris en charge : erreur qui cite les versions prises en charge.

## 3. Trouver un JDK

Gradle doit être lancé par un Java assez récent pour lui, et pas trop récent : chaque version de Gradle a une version de Java maximale.

### Compatibilité Gradle / Java

| Gradle à partir de | Java maximal |
| --- | --- |
| 9.1 | 25 |
| 8.14 | 24 |
| 8.10 | 23 |
| 8.8 | 22 |
| 8.5 | 21 |
| 8.3 | 20 |
| 7.6 | 19 |
| 7.5 | 18 |
| 7.3 | 17 |
| plus ancien | 16 |

Version de Gradle inconnue : le maximum le plus haut du tableau. **Java minimal : 17** dans tous les cas.

### Où chercher

Dans cet ordre, sans doublon :

1. `JAVA_HOME` ;
2. chaque sous-dossier des dossiers d'éditeurs de Java dans `Program Files` et `Program Files (x86)` : `Eclipse Adoptium`, `Java`, `Microsoft`, `Zulu`, `BellSoft`, `Amazon Corretto`, `Semeru`, `OpenJDK` ;
3. `~/.jdks` (téléchargés par IntelliJ) et `~/.gradle/jdks` (téléchargés par Gradle).

Un dossier est un JDK s'il contient `bin/java.exe` et un fichier `release` dont la ligne `JAVA_VERSION="21.0.5"` donne la version majeure (pour `1.8.0`, la version est 8).

### Choix

Parmi les JDK dont la version est comprise entre 17 et le maximum de Gradle : celui de la version demandée par le MDK, sinon le plus récent. Si ce n'est pas la version du MDK, un avertissement explique que Gradle téléchargera lui-même le Java de compilation (« toolchain »). Si aucun JDK ne convient : « Aucun JDK entre 17 et N n'a été trouvé (Gradle X). Installez un JDK N (par exemple Eclipse Temurin) puis relancez le build. »

## 4. Exécuter Gradle

- **Commande** : le wrapper du projet (`gradlew.bat` ; `gradlew` sur Linux et macOS), dans le dossier du projet, avec les tâches du backend puis `--console=plain`. La variable d'environnement `JAVA_HOME` est réglée sur le JDK choisi. Le journal affiche `> gradlew build --console=plain  [JDK 21 (C:\...)]`.
- **Sortie** : chaque ligne (sortie standard et erreur) est débarrassée des codes couleur ANSI (que certains outils NeoForge émettent malgré `--console=plain`), relayée au journal et conservée pour l'analyse des erreurs. Niveau d'une ligne : erreur si elle contient `error:` (ou sa version française) ou commence par `FAILURE` ; succès si elle commence par `BUILD SUCCESSFUL` ; avertissement si elle vient de la sortie d'erreur ; info sinon.
- **Annulation** : Gradle et **tous ses processus enfants** (le démon, et Minecraft pour `runClient`) sont arrêtés. Un avertissement « Arrêt de Gradle demandé » est écrit.
- **Résultat** : le code de sortie du processus.

Le premier build est long (téléchargement de Gradle, de Minecraft et des dépendances) : la console doit le dire à l'utilisateur.

## 5. Ramener les erreurs Java aux éléments

Après Gradle, chaque ligne conservée qui a la forme du compilateur Java

```
<fichier>.java:<ligne>: error: <message>
```

(ou `warning`, ou leur version française) devient un diagnostic :

- gravité : erreur ou avertissement ;
- message : « Java : » suivi du message du compilateur ;
- fichier : le chemin relatif au projet, et la ligne (un double-clic dans les Diagnostics ouvre le fichier à cette ligne) ;
- **source** : si la carte des sources de la génération connaît cette ligne, l'élément d'origine (« Item 'magic_sword' », « Graphe 'magic_wand', nœud ... ») et son identifiant interne ; un double-clic ouvre alors l'élément. Sinon, la source est `fichier:ligne`.

Les erreurs rattachées à un élément sont répétées dans le journal sous la forme « Origine : ... ». Elles apparaissent aussi en marge de l'éditeur de code quand le fichier est ouvert.

## 6. La chaîne complète

| Étape | Message dans la console | Échec si |
| --- | --- | --- |
| 1. Validation | `[1/4] Validation du projet <nom>` | pas de MDK utilisable, ou une erreur dans le projet |
| 2. Génération | `[2/4] Génération du projet <MDK>`, puis `<n> fichiers générés dans <dossier>` et, s'il y en a, le nombre de fichiers verrouillés conservés | une erreur de génération |
| 3. JDK | `[3/4] Recherche d'un JDK` | aucun JDK compatible |
| 4. Gradle | `[4/4] Compilation avec Gradle (le premier build peut prendre plusieurs minutes)` ou `Lancement de Minecraft...` | code de sortie non nul, ou .jar absent |

- **Fin réussie** : « Build réussi en mm:ss : <chemin du .jar> », ou « Minecraft fermé (mm:ss) » après un lancement. Le .jar reste là où Gradle l'a produit : `build/libs/<modid>-<version>.jar`.
- **Échec** : « Build interrompu : <raison> ». Tous les diagnostics accumulés (validation, génération, compilateur) sont transmis au panneau Diagnostics, en remplacement des résultats du build précédent.
- Le résultat garde la durée, le chemin du .jar et les diagnostics. La barre de statut affiche « Build réussi le <date> en mm:ss : <fichier> » ou « Échec du build le <date> ».

## 7. Ce qu'il faut savoir avant de réécrire cette partie

- **L'encodage de Gradle sous Windows** : sans `-Dfile.encoding=UTF-8` dans `org.gradle.jvmargs`, les accents de `mods.toml` sont corrompus avec Java 17.
- **`gradle.properties` est en ISO-8859-1** : écrire les caractères non ASCII en `\uXXXX`.
- **Arrêter Gradle, c'est arrêter un arbre de processus** : tuer le seul processus `gradlew` laisse le démon et Minecraft tourner.
- **Changer de MDK** dans un projet déjà compilé demande de vider `build/` et `.gradle/`, sinon Gradle mélange deux versions.
- **Les MDK NeoForge** ont leur `mods.toml` dans `src/main/templates`, hors des ressources : il faut le réécrire là.
- **Les dossiers de données ont changé de nom en 1.21** (singulier), et le format des résultats de recette en 1.20.5.
