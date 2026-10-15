# 01. Vision et périmètre

## En une phrase

Mine Engine est un environnement de création de mods Minecraft : on y décrit son contenu (items, blocs, créatures, recettes) et ses comportements (en graphes de nœuds, à la manière des Blueprints d'Unreal Engine), et il en fait un vrai mod Forge ou NeoForge, compilé et prêt à être lancé dans le jeu.

## Pourquoi ce projet existe

MCreator a montré qu'on pouvait créer des mods sans écrire de Java. Il a deux limites que Mine Engine veut dépasser :

1. **La logique se décrit par blocs empilés** (de type Blockly), ce qui devient vite illisible dès qu'un comportement dépasse quelques lignes. Mine Engine prend le modèle d'Unreal Engine : des nœuds reliés par des fils, avec de vraies fonctions, des classes, de l'héritage et des types.
2. **Le passage au code est brutal.** Mine Engine garde le projet ouvert sur le code : chaque projet est un vrai projet Gradle, le code Java écrit à la main y vit à côté du code généré, et un fichier généré peut être « verrouillé » pour être modifié à la main.

L'inspiration est donc double, et elle se retrouve partout dans l'interface et l'architecture : **MCreator** pour l'organisation des projets et les éditeurs d'éléments, **Unreal Engine** pour la programmation nodale et la disposition des panneaux, **VS Code** pour l'ergonomie de l'éditeur (explorateur, raccourcis, boutons de disposition).

## Pour qui

- **Un joueur qui veut créer son premier mod** sans connaître Java. Il doit pouvoir créer un item, un bloc, une recette et un comportement simple, compiler et tester en jeu sans jamais ouvrir un fichier de code.
- **Un créateur intermédiaire** qui dépasse les limites des outils à blocs : il structure sa logique en fonctions et en classes, réutilise ses définitions, range son projet en dossiers.
- **Un programmeur** qui veut aller plus vite : il garde la main sur le code Java, verrouille les fichiers qu'il retouche, et (à partir de la V1.1) publie ses propres méthodes Java comme nœuds utilisables par les autres.

Le public est francophone et anglophone : l'interface est en anglais par défaut, avec le français au choix.

## Ce que fait Mine Engine aujourd'hui (V0.4.5)

- **Gestion des projets** : un accueil liste les projets connus, en crée de nouveaux, en ajoute d'existants. Un projet est un dossier, qui est aussi le projet Gradle du mod.
- **Gestion des MDK** : téléchargement des MDK officiels pris en charge (Forge 1.20.1, Forge 1.21.1, NeoForge 1.21.1), import d'une archive ou d'un dossier, suppression.
- **Éléments du mod** : items (simples, outils, armes, armures, nourriture), blocs (forme, textures par face, récolte, butin), mobs (modèle, attributs, IA par comportements ordonnés, apparition, œuf, sons, butin), recettes (établi avec ou sans disposition, four), textures PNG et sons OGG importés.
- **Programmation nodale** : graphes d'événements (réactions au jeu), fonctions, procédures, classes (champs, constructeur, héritage, interfaces), structures, interfaces et énumérations. Les événements propres à un item (clic droit, frappe, consommation...) se relient à un graphe depuis l'éditeur de l'item.
- **Validation continue** : chaque élément est vérifié ; les problèmes s'affichent dans un panneau Diagnostics et sur l'élément lui-même.
- **Génération et compilation** : le projet est traduit en code Java et en ressources Minecraft, puis compilé par Gradle ; le mod peut être lancé directement dans le jeu. Les erreurs de compilation Java sont rattachées à l'élément qui les a causées.
- **Code Java** : un éditeur intégré (coloration, complétion, repli, formatage) pour le code de l'utilisateur et les fichiers générés, que l'on peut verrouiller.
- **Confort** : annulation de toute modification, sauvegarde automatique et sauvegardes de secours, dossiers logiques pour ranger les éléments, disposition des panneaux enregistrée, thème clair ou sombre, interface en anglais ou en français.

## Ce que Mine Engine ne fait pas (et ne doit pas faire sans décision explicite)

- **Il n'est pas un éditeur de modèles 3D ou de textures.** Les textures sont importées (PNG) ; les modèles de mobs sont ceux des créatures de Minecraft. L'import Blockbench est prévu en V1.4.
- **Il ne gère pas Fabric pour l'instant.** Un MDK Fabric peut être installé (il est reconnu), mais le générateur ne produit que du Forge et du NeoForge. Fabric est prévu en V1.6.
- **Il ne vise que les versions de Minecraft que son générateur connaît** : 1.20.1 et 1.21.1. Toute autre version demande un travail sur le générateur (voir [07](07-generation-du-mod.md)).
- **Il ne publie pas les mods** (CurseForge, Modrinth). Une icône est réservée pour cela, rien de plus.
- **Il ne réécrit jamais le code de l'utilisateur.** Seuls les fichiers qu'il a lui-même générés, et que l'utilisateur n'a pas verrouillés, sont réécrits ou supprimés.
- **Il ne fonctionne que sous Windows** dans sa version C#. La réécriture en Rust est l'occasion de viser aussi Linux et macOS (voir [12](12-portage-rust.md)), mais ce n'est pas encore une exigence.

## Principes qui guident toutes les décisions

1. **Aucun code requis.** Tout ce qu'un débutant fait doit être possible sans Java. Le code reste accessible, jamais imposé.
2. **Le projet est un vrai projet.** Un projet Mine Engine s'ouvre dans IntelliJ IDEA ou se compile en ligne de commande. Il n'existe pas de format caché que seul Mine Engine sait lire : les fichiers d'éléments sont du JSON lisible, versionnable dans Git.
3. **Tout est annulable.** Chaque modification faite dans l'éditeur passe par un historique ; Ctrl+Z la défait, y compris une suppression ou un déplacement de dossier.
4. **Les erreurs parlent la langue de l'utilisateur.** Un problème est expliqué en une phrase, rattaché à l'élément concerné, et propose quoi faire. Une erreur Java est ramenée à l'élément qui l'a produite.
5. **Les identifiants ne cassent pas les liens.** Les éléments se désignent entre eux par un identifiant interne stable ; renommer un élément ne casse rien.
6. **Les données de l'utilisateur sont sacrées.** Écritures atomiques, sauvegardes de secours, aucune suppression de fichier que Mine Engine n'a pas créé.
7. **Une interface calme.** Gris neutres, un seul accent bleu, icônes au trait, textes courts. Inspirée de VS Code et d'Unreal, pas d'un tableau de bord publicitaire.

## Critère de réussite de la V1.0

Sans écrire de Java, un utilisateur crée un mod, un item, un bloc, une recette et un comportement en nodal, compile, lance Minecraft et teste. Aucun bug majeur ne doit l'en empêcher.
