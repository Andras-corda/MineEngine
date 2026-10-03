# Roadmap Mine Engine

Ce fichier suit l'avancement du projet version par version. Le détail de chaque version (objectifs, périmètre, choix techniques) est dans le cahier des charges ; ici, on coche ce qui est fait.

Règle : une version n'est terminée que lorsque son **critère de réussite** est démontré.

| Version | Thème | État |
| --- | --- | --- |
| V0.1 | Prototype technique | En cours (critère de réussite validé) |
| V0.2 | Fondation de l'éditeur | En cours (critère à valider en jeu) |
| V0.3 | Système d'assets, mobs et IA | En cours (critère à valider en jeu) |
| V0.4 | Prototype nodal | À faire |
| V1.0 | Alpha exploitable | À faire |
| V1.1 | Code et custom nodes | À faire |
| V1.2 | Particules | À faire |
| V1.3 | Renderers | À faire |
| V1.4 | Animation | À faire |
| V1.5 | Entités et IA avancées | À faire |
| V1.6 | Plugins | À faire |

---

## V0.1 - Prototype technique

Objectif : prouver que la chaîne complète fonctionne, de l'application C# jusqu'au mod dans Minecraft.

**Critère de réussite :** créer un projet, créer un item avec sa texture, Build, Play : l'item est visible dans l'inventaire créatif, avec son nom et sa texture.

### Application
- [x] Fenêtre WPF et menu principal
- [x] Créer, ouvrir, enregistrer, fermer un projet
- [x] Fenêtre de création de projet : nom, identifiant, version, auteurs, site web, licence, description, MDK
- [x] Paramètres du projet modifiables après création
- [x] Console Output avec couleurs par niveau
- [x] Confirmation avant de perdre des modifications non enregistrées

### Contenu
- [x] Assets Item et Block
- [x] Inspector : nom, identifiant, taille de pile, dureté, résistance
- [x] Import de texture PNG avec aperçu
- [x] Texture de remplacement quand aucune n'est choisie

### MDK
- [x] Bibliothèque de MDK dans `%LOCALAPPDATA%\MineEngine\mdks`
- [x] Téléchargement des MDK officiels (Forge 1.20.1, Forge 1.21.1, NeoForge 1.21.1)
- [x] Import d'une archive .zip ou d'un dossier, détection automatique du loader et des versions
- [x] Détection des archives et dossiers déposés à la main
- [x] Gestionnaire de MDK dans l'éditeur

### Génération et build
- [x] Mod IR minimal (contenu déclaratif)
- [x] Backends Forge et NeoForge choisis selon le MDK du projet
- [x] Génération Java, modèles, blockstates, langues (en_us, fr_fr), loot tables
- [x] Recherche d'un JDK compatible avec la version de Gradle du MDK
- [x] Build Gradle et copie du .jar dans `Build/`
- [x] Lancement de Minecraft (runClient)
- [x] Build réel vérifié pour les trois MDK (tests de bout en bout)

### Reste à faire pour clore la V0.1
- [x] **Valider le critère de réussite en jeu** : lancer Minecraft depuis l'éditeur et voir l'item dans l'onglet créatif
- [x] Vérifier le correctif de l'erreur « ItemsControl incohérent » pendant un build réel
- [x] Fenêtre Paramètres avec un onglet Apparence : thème clair, sombre ou identique à Windows

---

## V0.2 - Fondation de l'éditeur

Objectif : transformer le prototype en véritable environnement de travail.

**Critère de réussite :** toute modification faite dans l'Inspector peut être annulée et rétablie ; une erreur de build s'affiche dans Output avec un lien vers sa cause.

- [x] Interface ModernWpf 1.0 (styles Windows 11, thèmes clair et sombre, barre d'outils avec icônes, champs numériques)
- [x] Panneaux ancrables (AvalonDock 5) : Contenu, Explorateur de projet, Accueil, Inspector, Output, Diagnostics
- [x] Disposition des panneaux enregistrée à la fermeture, menu Affichage pour les réafficher ou réinitialiser
- [x] Menus Fichier, Édition, Affichage, Projet, Build, Outils complets
- [x] Undo/redo généralisé (pattern Command dans le Core) : propriétés des assets, textures, ajout et suppression d'assets, paramètres du projet
- [x] Recherche et filtre par type dans le panneau Contenu
- [x] Panneau Diagnostics mis à jour à chaque modification, double-clic vers l'asset concerné
- [x] Remontée des erreurs `javac` vers l'asset d'origine (correspondance lignes générées / assets)
- [x] Sauvegarde automatique (intervalle réglable) et sauvegardes de secours (.zip dans .mineengine/backups)
- [x] Journal écrit dans un fichier (`%LOCALAPPDATA%\MineEngine\logs`) en plus de la console

### Rework des items et des blocs (inspiré de MCreator)
L'Inspector est organisé en onglets, comme les éditeurs d'items et de blocs de MCreator.

- [x] Item, onglet Visuel : texture, effet brillant
- [x] Item, onglet Propriétés : durabilité, rareté, taille de pile (forcée à 1 avec durabilité), résistance au feu, info-bulle multiligne, onglets créatifs vanilla
- [x] Item, onglet Nourriture : valeur nutritive, saturation, toujours mangeable, viande
- [x] Bloc, onglet Visuel : forme (cube, une texture par face, colonne type bûche, plante en croix), transparence (cutout, translucide), luminosité, textures par face
- [x] Bloc, onglet Propriétés : dureté, résistance, incassable, son, glissance, vitesse de marche, hauteur de saut
- [x] Bloc, onglet Récolte : outil efficace, niveau d'outil, outil requis, ce que laisse le bloc (lui-même, rien, un autre item avec quantité), expérience
- [x] Bloc, onglet Item : forme item activable, rareté, pile, info-bulle, onglets créatifs
- [x] Génération : tags `mineable/*` et `needs_*_tool`, loot tables, modèles et blockstates par forme, différences d'API entre 1.20.1 et 1.21 gérées
- [x] Anciens projets relus avec des valeurs par défaut

Repoussé à plus tard : génération de minerai dans le monde, rotation horizontale, gravité, inflammabilité, redstone, déclencheurs (procédures), modèles personnalisés.

Reste à faire pour clore la V0.2 :
- [ ] Valider le critère de réussite à la main, notamment sur un vrai build en erreur (la remontée `javac` n'est testée que par des tests automatiques)
- [ ] Vérifier en jeu les nouveaux items et blocs (nourriture, minerai qui laisse un item, bûche, fleur, bloc lumineux)

---

## V0.3 - Système d'assets, mobs et IA

Objectif : une base de contenu solide pour toutes les versions suivantes. Les mobs et leur IA, prévus en V1.5, sont avancés ici dans une première version déclarative (comme dans MCreator).

**Critère de réussite :** renommer une texture utilisée par trois items met à jour les trois items, et le mod compile toujours.

### Système d'assets
- [x] Nouveaux types d'assets : Texture, Sound, Recipe, Mob
- [x] La texture devient un asset référencé par `guid` : items, blocs et mobs la désignent par son identifiant interne
- [x] Les items désignés ailleurs (item laissé par un bloc, recettes, butin et IA des mobs) sont suivis par `guid` ; un nom libre (« diamond ») désigne un item de Minecraft
- [x] Références entre assets : « Utilisé par » dans l'Inspector, confirmation avant de supprimer un asset utilisé, références cassées signalées dans Diagnostics
- [x] Renommage sans casse : les références par `guid` survivent au changement d'identifiant
- [x] Identifiants uniques par famille (une texture peut porter le nom de l'item qui l'utilise)
- [x] Import de textures (PNG) et de sons (OGG), plusieurs fichiers à la fois, en une étape annulable
- [x] Fichiers d'assets rangés par type (`Content/Items`, `Content/Mobs`...)
- [x] Migration automatique des projets V0.1 et V0.2 (format 1) : textures transformées en assets, items laissés résolus, fichiers rangés à l'enregistrement

### Recettes
- [x] Établi avec disposition (motif réduit automatiquement), établi sans disposition, four (durée, expérience)
- [x] Ingrédients : items du projet, items de Minecraft ou tags (`#minecraft:planks`)
- [x] Format JSON propre à chaque version (1.20.1 et 1.21+)

### Sons
- [x] Déclaration dans `sounds.json`, enregistrement des événements sonores, sous-titres traduits
- [x] Utilisables par les mobs (ambiant, blessure, mort), comme les sons de Minecraft

### Mobs et IA
- [x] Familles : monstre, créature, animal (reproduction, bébés)
- [x] Apparence : 14 modèles de Minecraft (zombie, squelette, villageois, vache, araignée...), texture du projet ou d'origine, taille de collision
- [x] Attributs : vie, armure, vitesse, dégâts, portée de suivi, résistance au recul, expérience, résistance au feu, brûle au soleil, persistance
- [x] IA : liste ordonnée de 15 comportements (nager, paniquer, attaquer, bondir, se promener, regarder, suivre un item, se reproduire, suivre ses parents, fuir une créature, fuir le soleil, se rapprocher, riposter, cibler), avec leurs réglages et leur priorité
- [x] Apparition : œuf d'apparition (couleurs), apparition naturelle par biome (fréquence, taille des groupes)
- [x] Sons et butin (loot table)
- [x] Génération Forge 1.20.1, Forge 1.21.1 et NeoForge 1.21.1 vérifiée par un build Gradle réel

### Interface
- [x] Nouvel accueil (sélection de projet) : barre latérale (Accueil, Mes projets, Paramètres, MDK, Aide), liste des projets avec recherche, tri et onglet Récents, aperçu du projet sélectionné, ajout d'un projet existant, retrait de la liste
- [x] Liste des projets enregistrée dans `%LOCALAPPDATA%\MineEngine\projects.json` ; au premier lancement, les projets de `Documents\MineEngine Projects` y sont ajoutés
- [x] Couleur d'accent fixe (lilas), cartes et barre de statut harmonisées, menu Build renommé Compilation, version affichée depuis `Directory.Build.props`

Déplacé :
- Asset Graph : avec le node editor en V0.4.
- Asset Script : avec l'éditeur Java en V1.1.

Reste à faire pour clore la V0.3 :
- [ ] Valider le critère de réussite à la main dans l'éditeur, puis en jeu
- [ ] Vérifier les mobs en jeu (IA, apparition naturelle, reproduction, œufs) et les recettes

---

## V0.4 - Prototype nodal

Objectif : un premier node editor, volontairement réduit.

**Critère de réussite :** le graphe `PlayerInteract -> HasItem -> SpawnItem` fonctionne en jeu.

- [ ] Asset Graph (déplacé de la V0.3)
- [ ] Node editor (Nodify) : créer, déplacer, connecter, supprimer des nodes
- [ ] Catégories Event, Condition, Action, Variable
- [ ] Pins d'exécution et pins de données typés
- [ ] Sauvegarde et chargement des graphes
- [ ] Partie comportement du Mod IR (`IRBehavior`) et `GraphCompiler`
- [ ] Génération Java des événements pour Forge et NeoForge

---

## V1.0 - Alpha exploitable

**Critère de réussite :** sans écrire de Java, un utilisateur crée un mod, un item, un bloc, une recette et un comportement en nodal, compile, lance Minecraft et teste.

- [ ] Bibliothèque de nodes : Events, Conditions, Math, Variables, Player, World, Items, Blocks, Entities, Audio
- [ ] Variables et fonctions simples
- [ ] Debug : erreurs, warnings, logs, fichiers générés, erreurs reliées aux nodes

---

## V1.1 à V1.6

| Version | Contenu principal | Critère de réussite |
| --- | --- | --- |
| V1.1 | Asset Script (déplacé de la V0.3), éditeur Java, custom nodes `@MineNode`, API Mine Engine et bibliothèque `mineengine-runtime` | Un programmeur publie une méthode Java ; un designer l'utilise dans un graphe sans ouvrir le code |
| V1.2 | Éditeur de particules et preview temps réel | Un effet réglé dans la preview a le même rendu en jeu |
| V1.3 | Renderers visuels, surchargeables en Java | À définir à la fin de la V1.2 |
| V1.4 | Animation, import Blockbench | À définir |
| V1.5 | Entités et IA avancées : IA nodale, modèles Blockbench, attaque à distance, mobs volants et aquatiques, apprivoisement (la base déclarative est faite en V0.3) | À définir |
| V1.6 | Système de plugins, backend Fabric | À définir |

---

## Décisions en attente

- [ ] ModernWpf est utilisé en version 1.0.0-rc.1 (la branche 0.9 n'est plus maintenue) : passer à la 1.0 stable dès sa sortie.

- [ ] Passer de .NET 9 à .NET 10 (LTS) : .NET 8 et 9 ne sont plus maintenus à partir du 10 novembre 2026. Il faut installer le SDK .NET 10 puis changer une ligne dans `Directory.Build.props`.
- [ ] Installer un JDK 21 (Temurin) pour les MDK Minecraft 1.21.1, au lieu de laisser Gradle le télécharger.
- [ ] Élargir les versions prises en charge par le générateur (autres versions de Forge et NeoForge, puis Fabric).
- [ ] Dépôt Git : secondaire pour le moment, à mettre en place plus tard.
- [ ] Mettre à jour le cahier des charges et le diagramme UML : bibliothèque de MDK, backends par loader, `IFileEmitter` placé dans le package Minecraft.
