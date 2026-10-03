# Mine Engine

Environnement de création de mods Minecraft, en C# / WPF. Version en cours : **V0.3, système d'assets, mobs et IA** (textures et sons en assets référencés par guid, recettes, mobs avec IA, migration des anciens projets), après la V0.2 (interface ModernWpf, panneaux ancrables, annuler/rétablir, diagnostics, sauvegardes, items et blocs détaillés comme dans MCreator).

Objectif de la V0.1 : créer un projet, ajouter un item ou un bloc, générer le projet Minecraft, compiler, puis voir l'item dans Minecraft.

L'avancement version par version est suivi dans [ROADMAP.md](ROADMAP.md).

## Prérequis

- Windows 10 ou 11
- SDK .NET 9
- Un JDK 17 ou plus récent pour lancer Gradle. Idéalement la version demandée par le MDK (17 pour Minecraft 1.20.1, 21 pour 1.21.1) ; sinon Gradle la télécharge lui-même pour compiler le mod.
- Une connexion Internet pour télécharger les MDK et au premier build (Gradle et Minecraft).

## Démarrer

```bash
dotnet run --project src/MineEngine.App
```

Dans Visual Studio : clic droit sur **MineEngine.App** > **Définir comme projet de démarrage**, puis F5.

Tests :

```bash
dotnet test
```

Les tests de bout en bout (vrais MDK et builds Gradle, plusieurs minutes chacun) ne s'exécutent que si `MINEENGINE_E2E=1`.

## MDK

Un MDK (Mod Development Kit) est le projet Gradle officiel de Forge ou NeoForge pour une version de Minecraft. Chaque projet Mine Engine choisit un MDK, qui fixe son loader et sa version de Minecraft.

**Outils > Gestionnaire de MDK** permet de :

- télécharger un MDK officiel : Forge 1.20.1, Forge 1.21.1, NeoForge 1.21.1 ;
- importer une archive `.zip` de MDK ou un dossier déjà extrait ;
- supprimer un MDK.

On peut aussi déposer soi-même une archive `.zip` ou un dossier de MDK dans `%LOCALAPPDATA%\MineEngine\mdks`, puis cliquer sur **Actualiser**.

Tout MDK Forge, NeoForge ou Fabric peut être installé, mais le générateur ne produit du code que pour les trois versions ci-dessus. Les autres apparaissent comme « Non pris en charge ».

## Utilisation

1. **Outils > Gestionnaire de MDK** : installez au moins un MDK.
2. L'**accueil** liste vos projets (recherche, tri, récents) : double-cliquez pour en ouvrir un, ou **Ajouter un projet...** pour en référencer un existant. **Fichier > Fermer le projet** y ramène.
3. **Nouveau projet...** : nom, identifiant, version, auteurs, site web, licence, description et MDK. Tout reste modifiable dans **Projet > Paramètres du projet**, sauf l'identifiant.
4. Ajoutez du contenu (barre d'outils, menu **Projet** ou panneau Contenu) et réglez-le dans l'Inspector :
   - **Item** : Visuel, Propriétés, Nourriture ;
   - **Bloc** : Visuel, Propriétés, Récolte, Item ;
   - **Mob** : Apparence, Attributs, IA (comportements par ordre de priorité), Apparition (œuf, biomes), Sons et butin ;
   - **Recette** : établi avec ou sans disposition, ou four ;
   - **Importer** : des images PNG (assets Texture) et des sons OGG (assets Son), réutilisables partout.
5. **Compilation > Générer le mod** (Ctrl+Maj+B) : le `.jar` est copié dans le dossier `Build/` du projet.
6. **Compilation > Lancer Minecraft** (F5) : lance Minecraft avec le mod. Le contenu se trouve dans un onglet créatif portant le nom du mod.

## Éditeur

- **Panneaux** : Contenu (recherche et filtre), Explorateur de projet, Accueil, Inspector, Output, Diagnostics. Ils se déplacent, s'empilent ou se détachent ; la disposition est enregistrée. **Affichage > Réinitialiser la disposition** revient à l'origine.
- **Annuler / Rétablir** (Ctrl+Z / Ctrl+Y) : toute modification du projet, y compris l'ajout et la suppression d'assets et les paramètres du projet.
- **Références** : textures, sons et items sont désignés par leur identifiant interne ; on peut renommer un asset sans casser ce qui l'utilise. L'Inspector affiche « Utilisé par », et supprimer un asset utilisé demande confirmation.
- **Anciens projets** : un projet V0.1 ou V0.2 est converti à l'ouverture (ses textures deviennent des assets) ; enregistrez pour écrire le nouveau format.
- **Diagnostics** : mis à jour à chaque modification et après chaque build. Une erreur de compilation Java est rattachée à l'asset qui l'a produite ; un double-clic y mène.
- **Sauvegardes** : enregistrement automatique (5 minutes par défaut) et sauvegardes de secours dans `.mineengine\backups` du projet.
- **Journal** : la console Output est aussi écrite dans `%LOCALAPPDATA%\MineEngine\logs`.

## Paramètres

**Outils > Paramètres** (Ctrl+,) : onglet **Apparence** pour le thème clair, sombre ou identique à Windows ; onglet **Sauvegarde** pour l'intervalle de sauvegarde automatique et le nombre de sauvegardes de secours. Le choix s'applique immédiatement et est enregistré dans `%LOCALAPPDATA%\MineEngine\settings.json`.

## Architecture

```
src/
  MineEngine.Core        identifiants, Asset, historique annuler/rétablir, diagnostics, journaux, JSON, fichiers
  MineEngine.Assets      Item, Block, Mob (IA), Recipe, Texture, Sound, AssetRegistry, sérialisation, migration
  MineEngine.Project     ModProject, ProjectLayout, ProjectRepository, AssetFileImporter
  MineEngine.IR          Mod IR et traduction des assets (ModIRBuilder)
  MineEngine.Minecraft   MDK (bibliothèque, inspection, catalogue), backends Forge et NeoForge, ressources
  MineEngine.Generator   ModGenerator : Mod IR vers projet Gradle
  MineEngine.Build       BuildPipeline, JdkLocator, GradleRunner
  MineEngine.Editor      WPF (ModernWpf, AvalonDock) : vues, panneaux, ViewModels (MVVM), services
  MineEngine.App         exécutable et CompositionRoot
tests/
  MineEngine.Tests       tests xUnit
```

Règles :

- Seuls `MineEngine.Editor` et `MineEngine.App` dépendent de WPF.
- Le générateur ne lit que le Mod IR, jamais les assets directement.
- Tout ce qui est propre à un loader est derrière `IModLoaderBackend` ; `ModLoaderBackendRegistry` choisit le backend adapté au MDK du projet.
- Les dépendances sont passées par constructeur ; elles ne sont assemblées que dans `CompositionRoot`.

## Structure d'un projet de mod

```
MonMod/
  Project.json      paramètres du projet (dont le MDK choisi)
  Content/          un fichier .asset.json par asset, rangé par type (Items/, Blocks/, Mobs/, Recipes/, Textures/, Sounds/)
  Textures/         images PNG importées
  Sounds/           sons OGG importés
  Scripts/          réservé au code Java de l'utilisateur (V1.1)
  Graphs/           réservé aux graphes nodaux (V0.4)
  Generated/        projet Gradle généré à partir du MDK, réécrit à chaque build (run/ est conservé)
  Build/            mods .jar produits
```
