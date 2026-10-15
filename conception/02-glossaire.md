# 02. Glossaire

Les termes sont classés par thème. Le nom entre parenthèses est celui employé dans le code et les fichiers.

## Projet

**Projet (ModProject)**
: Un mod en cours de création. C'est un dossier sur le disque, qui contient le fichier projet, les éléments, les fichiers importés et le projet Gradle du mod.

**Fichier projet (`<Nom>.mineengine`)**
: Fichier JSON à la racine du projet : paramètres du mod, MDK choisi, dossiers du Dashboard, fichiers verrouillés.

**Paramètres du projet (ProjectSettings)**
: Nom, identifiant, version, auteurs, description, licence, site web du mod, et MDK utilisé.

**Identifiant du mod (ModId)**
: L'espace de noms du mod dans Minecraft (`monmod`). Il est choisi à la création et ne change plus.

**Projet connu (KnownProject)**
: Un projet présent dans la liste de l'accueil, avec sa date de dernière ouverture.

**Workspace**
: Le dossier où le mod est généré. Depuis la V0.4.2, c'est le dossier du projet lui-même.

## Éléments du mod

**Élément (Asset)**
: Tout ce que l'utilisateur crée ou importe dans son mod : item, bloc, mob, recette, texture, son, graphe. Chaque élément est un fichier `.asset.json`.

**Identifiant interne (`guid`)**
: Identifiant stable et unique d'un élément (un UUID). Il ne change jamais, même quand l'élément est renommé ; c'est lui que les autres éléments utilisent pour s'y référer.

**Identifiant en jeu (ResourceId)**
: Le nom de l'élément dans Minecraft (`magic_sword`, qui devient `monmod:magic_sword`). L'utilisateur peut le renommer.

**Identifiant complet (NamespacedId)**
: Un identifiant Minecraft avec son espace de noms (`minecraft:diamond`, `monmod:ruby`). Sans espace de noms, `minecraft` est sous-entendu.

**Référence de contenu (ContentReference)**
: Désigne un item, un bloc ou un son : un élément du projet (par son identifiant interne), un élément du jeu (par son identifiant complet), ou un tag d'items.

**Tag**
: Un groupe d'items défini par Minecraft ou un mod (`#minecraft:planks`). Il n'est accepté que comme ingrédient de recette ; un item peut aussi déclarer à quels tags il appartient.

**Forme item**
: L'existence d'un bloc dans l'inventaire. Un bloc peut ne pas en avoir (comme un portail).

**Dossier (du Dashboard)**
: Un rangement logique des éléments, propre à l'éditeur (`Armes/Épées`). Il ne change ni le jeu, ni l'emplacement des fichiers.

**Lien / « Utilisé par » (AssetReference)**
: Le fait qu'un élément en désigne un autre (texture d'un item, ingrédient d'une recette). Sert à afficher « Utilisé par » et à signaler un lien cassé.

## Programmation nodale

**Graphe (GraphAsset)**
: Un élément qui contient des nœuds reliés par des fils. Selon sa sorte, il décrit des réactions aux événements du jeu, une fonction, une procédure ou un type.

**Sorte de graphe (GraphKind)**
: Ce que définit un graphe : `Events`, `Function`, `Procedure`, `Class`, `Struct`, `Interface` ou `Enum`.

**Définition**
: Un graphe qui n'est pas un graphe d'événements : fonction, procédure, classe, structure, interface ou énumération.

**Nœud (GraphNode)**
: Une étape placée sur le canevas : événement, condition, action, valeur, appel, création d'objet...

**Broche (PinInfo)**
: Une entrée ou une sortie d'un nœud. Une broche d'exécution porte l'ordre d'exécution ; une broche de valeur porte une donnée d'un certain type.

**Fil (GraphConnection)**
: Un lien entre la sortie d'un nœud et l'entrée d'un autre. Le fil d'exécution est blanc, les fils de valeur ont la couleur de leur type.

**Fil d'exécution / flux (NodeFlow)**
: La place d'un nœud dans l'ordre d'exécution : point de départ (`Start`), instruction (`Step`), condition (`Branch`), fin (`End`) ou valeur calculée à la demande (`Pure`).

**Nœud pur**
: Un nœud sans fil d'exécution, recalculé là où sa sortie est utilisée (comme dans les Blueprints).

**Type de donnée (DataType)**
: Le type d'une valeur : un type intégré (nombre, texte, booléen, joueur, position, item, bloc, son) ou un type défini dans le projet (classe, structure, interface, énumération).

**Membre**
: Une méthode ou un type imbriqué intégré à une classe (le graphe a alors un propriétaire, `owner`).

**Moi (Self)**
: L'objet dont la méthode ou le constructeur s'exécute (`this` en Java).

**Événement d'item**
: Un événement propre à un item du mod (clic droit avec l'item, item mangé...). Son code est ajouté à la classe Java de l'item.

**Modèle de programme (ProgramModel)**
: Une vue de toutes les définitions d'un projet à un instant donné, qui sait décrire les nœuds, la visibilité et l'héritage.

## Génération et compilation

**MDK (Mod Development Kit)**
: Le projet Gradle officiel d'un loader (Forge, NeoForge) pour une version de Minecraft. Mine Engine en recopie les fichiers de build dans chaque projet.

**Bibliothèque de MDK**
: Le dossier où Mine Engine range les MDK installés (`%LOCALAPPDATA%\MineEngine\mdks`).

**Loader**
: Le chargeur de mods visé : Forge ou NeoForge (Fabric reconnu, pas encore généré).

**Backend**
: La partie du générateur propre à un loader et à une version de Minecraft.

**Mod IR (représentation intermédiaire)**
: La traduction du projet en une structure indépendante du loader, seule entrée du générateur.

**Abaissement (lowering)**
: La traduction d'un élément du projet en sa forme IR.

**Émetteur (IFileEmitter)**
: Un composant qui produit une famille de fichiers du mod (modèles de blocs, recettes, code Java...).

**Fichier généré**
: Un fichier écrit par Mine Engine dans le workspace. La liste en est tenue dans `.mineengine/generated.json`.

**Fichier verrouillé**
: Un fichier généré que l'utilisateur a choisi de modifier à la main. Mine Engine ne le réécrit plus.

**Carte des sources (SourceMap)**
: La correspondance entre les lignes des fichiers Java générés et les éléments du projet ; elle rattache une erreur de compilation à l'élément qui l'a causée.

**Texture de remplacement**
: Le damier magenta et noir de 16 × 16 pixels utilisé quand un élément n'a pas de texture.

## Éditeur

**Accueil (Hub)**
: L'écran affiché quand aucun projet n'est ouvert.

**Dashboard**
: L'onglet permanent qui liste les éléments du mod, en liste ou en vignettes, avec filtres, recherche et dossiers.

**Panneau Détails (Inspecteur)**
: Le panneau de droite qui montre la cible sélectionnée (un élément, un fichier, un nœud) et ses propriétés.

**Explorateur**
: Le panneau de gauche qui montre l'arborescence des fichiers du projet, comme VS Code.

**Diagnostic**
: Un problème trouvé par la validation, la génération ou la compilation : gravité (erreur, avertissement, info), message, élément ou fichier concerné.

**Commande annulable (IUndoableCommand)**
: Une modification qui sait s'appliquer et s'annuler. Toute modification passe par l'historique sous cette forme.
