# Dossier de conception de Mine Engine

Ce dossier décrit Mine Engine assez précisément pour le reconstruire de zéro, dans n'importe quel langage. Il a été écrit à partir de la version 0.4.5 (écrite en C# et WPF), au moment où le projet s'apprête à être réécrit en Rust.

Il ne parle presque pas de C#. Il dit ce que le logiciel fait, pourquoi il le fait ainsi, et quelles règles il doit respecter. Quand un détail de l'implémentation actuelle compte pour la suite (un format de fichier, un piège rencontré), il est donné explicitement.

## Ordre de lecture

| N° | Document | Contenu |
| --- | --- | --- |
| 01 | [Vision et périmètre](01-vision-et-perimetre.md) | Ce qu'est Mine Engine, pour qui, ce qu'il fait et ne fait pas |
| 02 | [Glossaire](02-glossaire.md) | Les mots du projet, dans le sens où ils sont employés partout ailleurs |
| 03 | [Architecture](03-architecture.md) | Les modules, leurs dépendances et le trajet d'un mod, de l'éditeur au fichier .jar |
| 04 | [Modèle du domaine](04-modele-du-domaine.md) | Identifiants, éléments du mod, propriétés, bornes, valeurs par défaut et règles de validation |
| 05 | [Programmation nodale](05-programmation-nodale.md) | Graphes, nœuds, broches, types, fonctions, classes, et toutes les règles qui s'y appliquent |
| 06 | [Formats de fichiers](06-formats-de-fichiers.md) | Organisation d'un projet sur le disque et schéma de chaque fichier lu ou écrit |
| 07 | [Génération du mod](07-generation-du-mod.md) | Représentation intermédiaire, ressources Minecraft et code Java produits |
| 08 | [MDK, JDK et compilation](08-mdk-jdk-et-compilation.md) | Bibliothèque de MDK, recherche de Java, exécution de Gradle et lecture de ses erreurs |
| 09 | [Interface utilisateur](09-interface-utilisateur.md) | Écrans, panneaux, éditeurs, gestes, raccourcis, couleurs, police et icônes |
| 10 | [Comportements transverses](10-comportements-transverses.md) | Annulation, sauvegardes, journal, diagnostics, langues, gestion des erreurs |
| 11 | [Tests et qualité](11-tests-et-qualite.md) | Ce qui doit être testé et comment, jeu d'exemple, test de bout en bout |
| 12 | [Portage en Rust](12-portage-rust.md) | Correspondance des modules, choix de bibliothèques, ordre de réalisation |
| 13 | [Décisions](13-decisions.md) | Les choix structurants et leurs raisons, pour ne pas les rediscuter sans le savoir |
| 14 | [Feuille de route](14-feuille-de-route.md) | Versions passées et à venir, limites connues, questions ouvertes |

Le style des icônes est décrit dans [docs/ICONS.md](../docs/ICONS.md), et la liste des icônes dans `src/MineEngine.Editor/Themes/Icons.xaml`. Ces deux fichiers restent la référence.

## Conventions de ces documents

- **Les noms de code sont en anglais** (`ItemAsset`, `GraphNodeKind.OnUseItem`, `resourceId`). Ce sont ceux des fichiers enregistrés et de l'implémentation actuelle ; les garder évite toute conversion des projets existants.
- **Les textes de l'interface sont cités en français**, la langue dans laquelle ils sont écrits dans le code ; leur traduction anglaise est dans `src/MineEngine.Core/Localization/en.json`.
- **« Doit » signale une règle**, dont l'oubli casse un projet ou le mod produit. **« Peut » signale un choix d'implémentation** laissé libre.
- **Les valeurs par défaut et les bornes** sont données entre parenthèses ou dans des tableaux ; ce sont celles du code actuel.
- **Un élément** désigne ce que l'utilisateur crée dans son mod (item, bloc, mob, recette, texture, son, graphe). Le code l'appelle « asset » ; les deux mots désignent la même chose.

## Ce que ce dossier remplace

La ROADMAP parlait d'un « cahier des charges » qui n'a jamais été versionné. Ce dossier en tient lieu : en cas de doute, c'est lui qui fait foi, puis le code de la version 0.4.5.
