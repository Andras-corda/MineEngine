# 12. Portage en Rust

Ce document aide à réécrire Mine Engine en Rust : comment découper le projet, comment traduire les modèles C# dans un style Rust naturel, quelles bibliothèques envisager, dans quel ordre avancer, et quels pièges éviter. Les choix de bibliothèques sont des recommandations, à valider au moment de commencer (leurs versions et leur maturité évoluent vite).

## 1. Objectifs du portage

1. **Compatibilité des projets** : ouvrir les projets créés par la version C# sans conversion, et les réenregistrer à l'octet près (voir [06](06-formats-de-fichiers.md)).
2. **Même mod produit** : pour un même projet et un même MDK, les fichiers générés doivent être équivalents (idéalement identiques) à ceux de la version 0.4.5.
3. **Même comportement** : les règles de [04](04-modele-du-domaine.md), [05](05-programmation-nodale.md) et [10](10-comportements-transverses.md).
4. **Multiplateforme si possible** : Windows d'abord, mais sans fermer la porte à Linux et macOS (chemins, `gradlew` sans `.bat`, `bin/java` sans `.exe`, dossier de données de l'application).

## 2. Découpage en crates

Un workspace Cargo qui reprend les modules ([03](03-architecture.md)) et leurs règles de dépendance :

```
mineengine/
  Cargo.toml                 workspace
  crates/
    me-core/                 identifiants, références, dossiers, diagnostics, annulation, journal, I/O atomique, JSON, traductions
    me-assets/               éléments, validation, (dé)sérialisation, registre, graphes et modèle de programme
    me-project/              disposition, paramètres, dépôt, import, sauvegardes, dossiers, verrous, projets connus
    me-ir/                   Mod IR, abaissements, compilateur de graphes
    me-minecraft/            versions, MDK, backends, émetteurs (ressources, Java), carte des sources
    me-generator/            orchestration d'une génération
    me-build/                JDK, Gradle, lecture des erreurs, chaîne complète
    me-editor/               état de l'interface (view models), indépendant du rendu
    me-ui/                   rendu (dépend de la bibliothèque graphique choisie)
    mineengine/              binaire : racine de composition
  tests/                     tests d'intégration et de bout en bout
  assets/                    polices, icônes, traductions
```

Séparer `me-editor` (état et commandes de l'interface, testable sans fenêtre) de `me-ui` (le dessin) permet de changer de bibliothèque graphique sans tout réécrire, et de tester l'interface comme du code ordinaire.

## 3. Traduire le modèle dans un style Rust

### Identifiants et valeurs

- Des types « nouveau type » qui ne peuvent être construits que valides : `struct ResourceId(String)`, `struct ModId(String)`, `struct NamespacedId { namespace: String, path: String }`, avec `TryFrom<&str>` et `Display`. L'identifiant interne : `uuid::Uuid` enveloppé dans `struct AssetId(Uuid)`.
- `ContentReference` : une énumération `Asset(AssetId) | Id(NamespacedId) | Tag(NamespacedId)`, avec `FromStr` et `Display` sur sa forme texte.

### Les éléments

La hiérarchie de classes C# (`Asset` > `TexturedAsset` > `InventoryAsset` > `ItemAsset`) devient de la composition :

```rust
pub struct AssetHeader { id: AssetId, resource_id: ResourceId, display_name: String, folder: FolderPath }

pub enum AssetBody {
    Item(ItemAsset), Block(BlockAsset), Mob(MobAsset), Recipe(RecipeAsset),
    Texture(TextureAsset), Sound(SoundAsset), Graph(GraphAsset),
}

pub struct Asset { header: AssetHeader, body: AssetBody }

pub struct InventoryProps { texture: Option<AssetId>, max_stack_size: u8, rarity: ItemRarity, /* ... */ }
pub struct ItemAsset { inventory: InventoryProps, kind: ItemKind, /* ... */ }
pub struct BlockAsset { inventory: InventoryProps, model: BlockModelKind, /* ... */ }
```

La validation et la liste des liens deviennent des fonctions (ou un trait implémenté par chaque corps) qui renvoient des `Vec<Diagnostic>` et des `Vec<AssetLink>`. Les bornes se garantissent dans des setters ou des constructeurs (`fn set_max_stack_size(&mut self, value: i32)` qui ramène dans [1, 99]).

### Les événements de modification

Le modèle C# signale chaque modification par un événement. En Rust, deux approches plus simples :

- **un compteur de révision** par projet (et par élément), incrémenté à chaque modification ; l'interface compare les révisions pour savoir quoi recalculer ;
- en interface immédiate (egui), rien à signaler : tout est redessiné à chaque image depuis l'état courant, et les calculs coûteux (validation) sont mis en cache par révision.

### L'historique d'annulation

Les commandes C# capturent des fermetures qui lisent et écrivent une propriété. En Rust, ce schéma se heurte aux règles d'emprunt. Deux approches sûres :

- **Instantanés** (recommandé pour commencer) : une commande garde la valeur avant et la valeur après d'un élément entier (les éléments sont petits) et le remplace dans le registre. La fusion des frappes se fait sur la clé (élément, champ, délai de 2 secondes). Les opérations composées (dossiers, import) sont des listes d'instantanés.
- **Commandes typées** : une énumération `Edit` (`SetDisplayName { id, old, new }`, `SetItemDurability { ... }`...), plus verbeuse, plus économe.

```rust
pub trait Command {
    fn description(&self) -> String;
    fn apply(&self, project: &mut Project);
    fn revert(&self, project: &mut Project);
    fn try_merge(&mut self, next: &dyn Command) -> bool { false }
}
```

Les graphes, déjà immuables en C#, se prêtent bien aux instantanés : `Arc<NodeGraph>` partagé entre l'élément et l'historique, ou une structure persistante (crate `im`).

### Fils d'exécution

Le projet vit sur le fil de l'interface. Pour compiler : **cloner le projet** (ou construire l'IR sur le fil de l'interface, puis l'envoyer), lancer la génération et Gradle sur un fil de travail, et remonter les lignes du journal par un canal (`std::sync::mpsc` ou `crossbeam-channel`). L'annulation passe par un drapeau partagé (`Arc<AtomicBool>`) ou un jeton d'annulation. Un runtime asynchrone (`tokio`) n'est utile que pour les téléchargements et l'attente de processus ; des fils ordinaires suffisent.

## 4. Bibliothèques envisagées

| Besoin | Recommandation | Remarque |
| --- | --- | --- |
| JSON | `serde`, `serde_json` avec la fonction `preserve_order` | voir les pièges de formatage, section 7 |
| UUID | `uuid` (fonctions `v4`, `serde`) | |
| Erreurs | `thiserror` dans les bibliothèques, `anyhow` dans le binaire | |
| Accents (NFD) | `unicode-normalization` | règle de nettoyage des identifiants |
| Expressions régulières | `regex` | erreurs javac, inspection des MDK |
| Archives | `zip` | MDK, sauvegardes |
| Empreinte | `sha2` | nom des fichiers importés |
| Image PNG | `png` (ou l'encodeur maison de 70 lignes, à porter) | texture de remplacement, lecture des dimensions |
| HTTP | `ureq` (simple, bloquant) ou `reqwest` | téléchargement des MDK |
| Processus | `std::process`, plus `command-group` ou un job object Windows (`win32job`) | **arrêter tout l'arbre** de processus de Gradle |
| Dossiers système | `dirs` ou `directories` | `%LOCALAPPDATA%`, `Documents` |
| Corbeille | `trash` | suppression depuis l'Explorateur |
| Surveillance du disque | `notify` | arborescence de l'Explorateur |
| Journal | écriture maison (format imposé, section 7 de [06](06-formats-de-fichiers.md)), ou `tracing` avec un formateur sur mesure | |
| Tests | `cargo test`, `insta` pour les instantanés de fichiers générés, `tempfile` | |

### Interface graphique

C'est le choix le plus structurant. L'éditeur a besoin de panneaux ancrables, d'un éditeur nodal, d'un éditeur de code avec complétion, de thèmes et d'une police intégrée.

| Option | Pour | Contre |
| --- | --- | --- |
| **egui / eframe** avec `egui_dock` (panneaux), `egui-snarl` ou un éditeur nodal maison (le canevas est simple en mode immédiat), `egui_extras` + `syntect` (coloration) | tout en Rust, rapide à prototyper, thèmes et polices faciles, éditeur nodal naturel en mode immédiat | éditeur de code limité (complétion, repli, recherche à écrire), rendu moins « natif » |
| **Tauri 2** (interface web, cœur en Rust) avec `dockview` (panneaux), React Flow / xyflow (nœuds), CodeMirror 6 ou Monaco (code) | composants très aboutis pour les trois besoins difficiles, look facile à reproduire en CSS | deux langages (TypeScript pour l'interface), pont de messages à maintenir |
| **Slint** | déclaratif, bon rendu, léger | pas de panneaux ancrables ni d'éditeur nodal prêts à l'emploi |
| **iced** | architecture Elm propre | idem, écosystème de composants d'éditeur encore mince |

**Recommandation** : garder tout le domaine en Rust dans tous les cas (crates `me-core` à `me-editor`). Pour le rendu, **egui** si l'objectif est un projet entièrement en Rust : l'éditeur nodal et les panneaux sont à portée, et l'éditeur de code peut commencer simple (coloration, numéros de ligne, recherche) puis s'étoffer. **Tauri** si l'éditeur de code (complétion, repli, formatage) doit être au niveau de la version C# dès le départ. Faire un prototype d'une semaine avec l'option choisie, centré sur l'éditeur nodal, avant de s'engager.

### Polices et icônes

- **Roboto** s'intègre directement au binaire (`include_bytes!`), avec sa licence (Apache 2.0).
- **Les glyphes de Segoe Fluent Icons ne sont pas redistribuables** et n'existent que sous Windows. Il faut une police d'icônes libre embarquée, qui couvre les clés de `Icons.xaml` : **Fluent UI System Icons** de Microsoft (licence MIT, même style que Segoe Fluent) est le choix le plus proche ; Lucide ou Phosphor sont des alternatives. Garder les **noms** d'icônes de `Icons.xaml` (`Glyph.Save`, `BlockGeometry`...) comme contrat.
- Les **géométries vectorielles** de `Icons.xaml` sont des chemins SVG valides sur un repère 16 × 16 : elles se transposent telles quelles en SVG (`<path d="..."/>`, contour seul, épaisseur 1,2 à 1,5).

### Traductions

Le plus simple est de garder le principe actuel : textes français dans le code, macro `tr!("Nouveau bloc")` et `tr!("{0} éléments", n)`, catalogue `en.json` réutilisé tel quel. Le test qui vérifie la couverture se réécrit en parcourant les sources Rust. Si l'on préfère des clés neutres, **Fluent** (`fluent-bundle`) gère bien les pluriels et l'ordre des arguments ; il faut alors convertir `en.json` et garder les mêmes contrôles automatiques ([10](10-comportements-transverses.md), section 6).

## 5. Ordre de réalisation conseillé

Chaque étape se termine par des tests verts, et les étapes 1 à 5 n'ont pas d'interface.

1. **Socle** : identifiants, références, dossiers, diagnostics, I/O atomique, format JSON. Tests d'identifiants.
2. **Éléments et projet** : modèle complet, lecture et écriture, registre, validation, annulation. **Test de compatibilité** : ouvrir les projets d'exemple créés par la version C# (dans `%TEMP%\mineengine-e2e`) et vérifier qu'un réenregistrement ne change aucun octet.
3. **Graphes** : modèle de programme, validation, palette. Porter le programme d'exemple des tests C#.
4. **IR et génération des ressources** : abaissements, émetteurs de ressources, liste des fichiers générés. **Comparer** les fichiers produits à ceux de la version C# pour les mêmes projets.
5. **Code Java, MDK, JDK, Gradle** : émetteurs Java, bibliothèque de MDK, recherche de JDK, exécution de Gradle, carte des sources. Atteindre les trois tests de bout en bout.
6. **Interface minimale** : accueil, ouverture d'un projet, Dashboard, éditeurs d'éléments, Diagnostics, Sortie, compilation.
7. **Éditeur nodal** et panneau Définition.
8. **Éditeur de code** et Explorateur.
9. **Finitions** : disposition enregistrée, thèmes, langues, icônes, sauvegardes, préférences.

Le portage est terminé quand les 121 tests de la version C# ont leur équivalent et que les projets d'exemple se compilent à l'identique.

## 6. Ce qu'il ne faut pas porter tel quel

- **Les migrations des anciens formats** (projets antérieurs à la 0.4.2, éléments au format 1) : elles peuvent être abandonnées, en demandant d'ouvrir d'abord ces projets avec la version C# 0.4.5.
- **Le fichier de disposition `layout.v2.xml`** : propre à la bibliothèque de panneaux WPF. Choisir un format adapté à la nouvelle bibliothèque, sous un autre nom.
- **Les détails propres à WPF** : convertisseurs, styles ModernWpf, contournements d'AvalonDock (bouton de liste des documents masqué).

## 7. Pièges connus

### Formatage JSON

La compatibilité à l'octet exige de reproduire exactement l'écriture de `System.Text.Json` :

- indentation de 2 espaces, fins de ligne LF, une fin de ligne finale (à ajouter : `serde_json` n'en met pas) ;
- caractères non ASCII non échappés (comportement par défaut de `serde_json`) ;
- **nombres** : `System.Text.Json` écrit un `double` entier sans partie décimale (`20`, pas `20.0`) alors que `serde_json` écrit `20.0` pour un `f64`. Il faut un sérialiseur de nombres sur mesure (ou stocker ces valeurs comme `serde_json::Number` construits avec soin). Les `float` (f32) sont écrits avec la plus courte représentation qui redonne la même valeur (`0.3`, pas `0.30000001192092896`) : utiliser le formatage `f32` de Rust (ryu), pas une conversion en `f64` ;
- ordre des clés (fonction `preserve_order`, ou des structures `serde` dont l'ordre des champs suit [06](06-formats-de-fichiers.md)) ;
- propriétés à valeur `null` écrites explicitement là où la version C# les écrit (`"craftRemainder": null`), et omises là où elle les omet (graphes).

### Fichiers et processus

- **Comparaisons de chemins insensibles à la casse sous Windows** (fichiers générés, verrous, projets connus), sensibles ailleurs.
- **`gradlew.bat`** sous Windows, **`gradlew`** (exécutable) ailleurs ; **`bin/java.exe`** contre **`bin/java`**.
- **`JAVA_HOME`** doit être réglé pour le processus Gradle.
- **Tuer Gradle, c'est tuer un arbre** : le démon Gradle et Minecraft (`runClient`) sont des enfants.
- **Écriture atomique** : écrire dans `.tmp` puis renommer ; sous Windows, un renommage vers un fichier existant doit le remplacer (`std::fs::rename` le fait sur les systèmes récents ; vérifier).
- **Encodages** : `gradle.properties` en ISO-8859-1 avec `\uXXXX` ; `-Dfile.encoding=UTF-8` dans les arguments JVM de Gradle ; fichiers générés en UTF-8 sans BOM et LF.

### Code Java généré

- Littéraux numériques : un `float` Java au format `0.0#######` suivi de `f` (`0.6f`, `1.0f`), un `double` au format `0.0###` (`1.25`, `1.0`), un entier négatif entre parenthèses dans une expression (`(-3)`). Toujours avec le point comme séparateur décimal, quelle que soit la langue du système.
- Les chaînes Java doivent être échappées (guillemets, antislashs, caractères de contrôle).
- Les imports sont triés et dédupliqués ; ils dépendent du code écrit, donc le corps est écrit avant l'en-tête, et la carte des sources décale ses numéros de ligne d'autant.

### Règles de nommage

- Nettoyage des identifiants (NFD, minuscules, `_`, préfixe `id_`) et noms de programme (`pointsDeVie`, `TRES_RARE`) : reprendre exactement les règles de [04](04-modele-du-domaine.md) et [05](05-programmation-nodale.md), avec leurs exemples comme tests.
