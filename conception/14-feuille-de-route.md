# 14. Feuille de route

## État au moment de la réécriture

| Version | Thème | État |
| --- | --- | --- |
| V0.1 | Prototype technique | Fini (critère de réussite validé) |
| V0.2 | Fondation de l'éditeur | Fini (critère de réussite validé) |
| V0.3 | Système d'assets, mobs et IA (simple) | En cours : vérifications en jeu à faire |
| V0.4 | Prototype nodal | En cours : critère à valider en jeu |
| V0.4.1 | Items complets, dossiers, onglets d'édition | En cours : panneau Détails à réduire |
| V0.4.2 | Architecture façon MCreator, éditeur Java, thème sombre et bleu | En cours : deux fonctions de navigation à faire |
| V0.4.3 | Programmation en graphes | En cours : boucles, listes, types abstraits à faire |
| V0.4.4 | Refonte graphique | Fini |
| V0.4.5 | Nouvelles icônes et police d'écriture | Fini |
| V0.5 | Paramétrage plus poussé de l'interface | À faire |
| V0.5.1 | Changement et importation de thèmes | À faire |
| V0.5.2 | Changement et importation de langues | En partie fait (anglais et français) |
| V0.5.3 | Changement de la disposition par glisser-déposer (système Unreal) | À faire |
| V0.6 | Intégration du sous-programme Puppeteer (arbres d'IA) | À faire |
| V0.6.1 | Corrections de bugs | À faire |
| V1.0 | Alpha exploitable | À faire |
| V1.1 à V1.6 | Code et nœuds personnalisés, particules, rendus, animation, entités avancées, plugins | À faire |

## Ce que chaque version a apporté

Cette section garde la mémoire de ce qui a été construit, pour vérifier que la réécriture n'oublie rien.

### V0.1, prototype technique

**Critère** : créer un projet, un item avec sa texture, compiler, lancer : l'item est dans l'inventaire créatif, avec son nom et sa texture.

Fenêtre et menus ; créer, ouvrir, enregistrer, fermer un projet ; paramètres du projet ; console colorée ; confirmation avant de perdre des modifications. Items et blocs, inspecteur, import de texture, texture de remplacement. Bibliothèque de MDK, téléchargement des MDK officiels, import d'archive ou de dossier, détection des dépôts manuels, gestionnaire de MDK. Mod IR minimal, backends Forge et NeoForge, génération Java, modèles, états de blocs, langues, tables de butin, recherche de JDK, build Gradle, lancement de Minecraft, tests de bout en bout sur les trois MDK. Thème clair, sombre ou identique au système.

### V0.2, fondation de l'éditeur

**Critère** : toute modification peut être annulée et rétablie ; une erreur de build s'affiche avec un lien vers sa cause.

Panneaux ancrables et disposition enregistrée ; menus complets ; annulation généralisée ; recherche et filtre ; Diagnostics mis à jour à chaque modification ; erreurs `javac` ramenées à l'élément ; sauvegarde automatique et sauvegardes de secours ; journal sur disque. Refonte des items (visuel, propriétés, nourriture) et des blocs (forme, transparence, lumière, textures par face, propriétés physiques, récolte, forme item) sur le modèle de MCreator, avec les tags d'outils et les différences entre 1.20.1 et 1.21.

### V0.3, système d'assets, mobs et IA

**Critère** : renommer une texture utilisée par trois items met à jour les trois items, et le mod compile toujours.

Textures, sons, recettes et mobs deviennent des éléments ; références par identifiant interne ; « Utilisé par » et liens cassés ; identifiants uniques par famille ; import multiple ; conversion des projets V0.1 et V0.2. Recettes (établi avec et sans disposition, four, tags). Sons (sous-titres). Mobs : trois familles, 14 modèles, attributs, 15 comportements d'IA ordonnés, apparition, œuf, sons, butin. Nouvel accueil et liste des projets connus ; Explorateur façon VS Code ; Dashboard ; panneau Détails ; barre de statut ; interface adaptée aux fenêtres étroites ; un seul dictionnaire de styles.

### V0.4, prototype nodal

**Critère** : le graphe « clic droit sur un bloc, possède l'item, faire apparaître un item » fonctionne en jeu.

Graphes immuables et annulables ; éditeur nodal (créer, déplacer, relier, supprimer, dupliquer, sélection au rectangle, déplacement et zoom de la vue, menu d'ajout avec recherche, fil lâché dans le vide, valeurs sur les nœuds) ; quatre familles de nœuds ; broches typées et fils colorés ; validation complète ; partie comportement de l'IR et compilateur de graphes ; génération des événements sur les trois cibles.

### V0.4.1, items complets, dossiers, onglets d'édition

Types d'items (outils, armes, armures) ; nourriture complète (27 effets) ; données (item rendu, non réparable, combustible, tags, tenu comme un outil) ; événements d'item reliés à des graphes ; dossiers logiques et leur interface complète dans le Dashboard ; édition des éléments dans des onglets.

### V0.4.2, architecture façon MCreator

Le projet devient le projet Gradle du mod ; conversion des anciens projets ; liste des fichiers générés ; code verrouillé ; classes Java de l'utilisateur ; éditeur Java complet ; double-clic dans Diagnostics vers l'élément ou la ligne ; thème sombre et bleu ; boutons de disposition dans la barre de titre.

### V0.4.3, programmation en graphes

Fonctions, procédures, classes, structures, interfaces, énumérations ; panneau Définition ; héritage, interfaces, redéfinition ; visibilité ; nœuds de programmation ; nœuds de calcul ; génération dans le package `code` ; Retour arrière supprime les nœuds.

### V0.4.4 et V0.4.5, refonte graphique

Gris neutres contrastés, contrôles repeints, police Roboto, style et bibliothèque d'icônes (`docs/ICONS.md`, `Icons.xaml`), icônes vectorielles par sorte d'élément et par loader, accueil épuré avec barre latérale réductible, interface en anglais par défaut avec le français au choix.

## Reste à faire dans les versions en cours

- **V0.3** : valider le critère à la main ; vérifier les mobs en jeu (IA, apparition naturelle, reproduction, œufs) et les recettes.
- **V0.4** : valider le critère en jeu (clic droit sur un bloc avec trois rubis, un diamant apparaît).
- **V0.4.1** : réduire le panneau Détails à un résumé avec « Ouvrir l'éditeur ».
- **V0.4.2** : « Voir le code Java » depuis un élément du Dashboard, et verrouillage par élément (tous ses fichiers) ; aller à la ligne (Ctrl+G), ouverture rapide de fichiers et d'éléments (Ctrl+P).
- **V0.4.3** : boucles (pour, tant que), listes, conversion entre types (« est un », transtypage), nombres décimaux ; classes et méthodes abstraites, surcharge ; appeler une méthode Java écrite à la main depuis un graphe (V1.1).
- **V0.5.2** : importer une langue (fichier de traductions ajouté par l'utilisateur) ; traduire les textes écrits dans le mod généré (description par défaut de `mods.toml`, nom des œufs d'apparition, commentaires du code Java).

## Versions à venir

| Version | Contenu | Critère de réussite |
| --- | --- | --- |
| V0.5 | Paramétrage plus poussé de l'interface | |
| V0.5.1 | Thèmes : changer, importer | |
| V0.5.2 | Langues : changer, importer | |
| V0.5.3 | Disposition par glisser-déposer, comme Unreal | |
| V0.6 | Intégration de Puppeteer (arbres d'IA) | |
| V0.6.1 | Corrections de bugs | |
| V1.0 | Alpha exploitable : bibliothèque de nœuds (événements, conditions, math, variables, joueur, monde, items, blocs, entités, audio), débogage relié aux nœuds | Sans écrire de Java, créer un mod, un item, un bloc, une recette et un comportement en nodal, compiler, lancer et tester, sans bug majeur |
| V1.1 | Nœuds personnalisés à la manière d'Unreal : une méthode Java annotée devient un nœud ; API Mine Engine et bibliothèque d'exécution | Un programmeur publie une méthode Java ; un designer l'utilise dans un graphe sans ouvrir le code |
| V1.2 | Éditeur de particules et aperçu en temps réel | Un effet réglé dans l'aperçu a le même rendu en jeu |
| V1.3 | Rendus visuels, surchargeables en Java | à définir |
| V1.4 | Animation, import Blockbench | à définir |
| V1.5 | Entités et IA avancées : IA nodale, modèles Blockbench, attaque à distance, mobs volants et aquatiques, apprivoisement | à définir |
| V1.6 | Système de plugins, backend Fabric | à définir |

## Reporté, pour mémoire

- **Blocs** : génération de minerai dans le monde, rotation horizontale, gravité, inflammabilité, redstone, déclencheurs, modèles personnalisés.
- **Items** : enchantements (enchantabilité, enchantements autorisés), matériaux d'armure et d'outil personnalisés, rendus et modèles 3D personnalisés, capabilities, échanges des villageois, composteur, distributeur ; relier un événement d'item à du code Java plutôt qu'à un graphe (V1.1).
- **Générateur** : autres versions de Forge et de NeoForge, puis Fabric.

## Questions ouvertes pour la réécriture

1. **Bibliothèque graphique** : egui (tout en Rust) ou Tauri (interface web) ? Voir [12](12-portage-rust.md). À trancher par un prototype de l'éditeur nodal.
2. **Langues** : garder les textes français dans le code et `en.json`, ou passer à des clés neutres et à un format comme Fluent ?
3. **Anciens formats** : reprendre les conversions des projets antérieurs à la 0.4.2, ou demander de les ouvrir d'abord avec la version C# ?
4. **Plateformes** : viser Linux et macOS dès la réécriture, ou plus tard ? (Impacts : police d'icônes à embarquer, recherche de JDK, `gradlew`, dossiers de l'application.)
5. **Format de la disposition des panneaux** : à redéfinir selon la bibliothèque choisie.
6. **Page Accueil** : elle est vide ; que doit-elle montrer (projets récents, actualités, tutoriels) ?
7. **Panneau Détails** : le réduire à un résumé (V0.4.1) maintenant que les éléments s'éditent dans des onglets ?
