# Mine Engine

Environnement de création de mods Minecraft, en C# / WPF. Cette version est la **V0.1 : prototype technique**.

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
2. **Fichier > Nouveau projet** : nom, identifiant, version, auteurs, site web, licence, description et MDK. Tout reste modifiable dans **Projet > Paramètres du projet**, sauf l'identifiant.
3. **+ Item** ou **+ Bloc**, puis réglez les propriétés dans l'Inspector (nom, identifiant, taille de pile, dureté, résistance, texture PNG).
4. **Build > Générer le mod** (Ctrl+Maj+B) : le `.jar` est copié dans le dossier `Build/` du projet.
5. **Build > Lancer Minecraft** (F5) : lance Minecraft avec le mod. Le contenu se trouve dans un onglet créatif portant le nom du mod.

## Paramètres

**Outils > Paramètres** (Ctrl+,), onglet **Apparence** : thème clair, sombre ou identique à Windows. Le choix s'applique immédiatement et est enregistré dans `%LOCALAPPDATA%\MineEngine\settings.json`.

## Architecture

```
src/
  MineEngine.Core        identifiants, Asset, diagnostics, journal, JSON, fichiers
  MineEngine.Assets      ItemAsset, BlockAsset, AssetRegistry, définitions, sérialisation
  MineEngine.Project     ModProject, ProjectLayout, ProjectRepository, TextureImporter
  MineEngine.IR          Mod IR et traduction des assets (ModIRBuilder)
  MineEngine.Minecraft   MDK (bibliothèque, inspection, catalogue), backends Forge et NeoForge, ressources
  MineEngine.Generator   ModGenerator : Mod IR vers projet Gradle
  MineEngine.Build       BuildPipeline, JdkLocator, GradleRunner
  MineEngine.Editor      WPF : vues, ViewModels (MVVM), services de dialogue
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
  Content/          un fichier .asset.json par asset
  Textures/         textures PNG importées
  Scripts/          réservé au code Java de l'utilisateur (V1.1)
  Graphs/           réservé aux graphes nodaux (V0.4)
  Generated/        projet Gradle généré à partir du MDK, réécrit à chaque build (run/ est conservé)
  Build/            mods .jar produits
```
