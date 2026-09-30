# Roadmap Mine Engine

Ce fichier suit l'avancement du projet version par version. Le détail de chaque version (objectifs, périmètre, choix techniques) est dans le cahier des charges ; ici, on coche ce qui est fait.

Règle : une version n'est terminée que lorsque son **critère de réussite** est démontré.

| Version | Thème | État |
| --- | --- | --- |
| V0.1 | Prototype technique | En cours (critère de réussite validé) |
| V0.2 | Fondation de l'éditeur | À faire |
| V0.3 | Système d'assets | À faire |
| V0.4 | Prototype nodal | À faire |
| V1.0 | Alpha exploitable | À faire |
| V1.1 | Code et custom nodes | À faire |
| V1.2 | Particules | À faire |
| V1.3 | Renderers | À faire |
| V1.4 | Animation | À faire |
| V1.5 | Entités et IA | À faire |
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

- [ ] Panneaux ancrables (AvalonDock) : Project Explorer, Content Browser, Workspace, Inspector, Output
- [ ] Menus File, Edit, View, Project, Build complets
- [ ] Undo/redo généralisé (pattern Command dans le Core)
- [ ] Panneau de diagnostics avec navigation vers la source
- [ ] Remontée des erreurs `javac` vers l'asset d'origine
- [ ] Sauvegarde automatique et sauvegardes de secours du projet
- [ ] Journal écrit dans un fichier en plus de la console

---

## V0.3 - Système d'assets

Objectif : une base de contenu solide pour toutes les versions suivantes.

**Critère de réussite :** renommer une texture utilisée par trois items met à jour les trois items, et le mod compile toujours.

- [ ] Types d'assets : Recipe, Texture, Sound, Graph, Script
- [ ] La texture devient un asset référencé par `guid` (au lieu d'un chemin)
- [ ] Références entre assets et détection des références cassées
- [ ] Renommage avec mise à jour automatique des références
- [ ] Recettes shaped et shapeless, générées en JSON
- [ ] Migration automatique des anciens formats de projet

---

## V0.4 - Prototype nodal

Objectif : un premier node editor, volontairement réduit.

**Critère de réussite :** le graphe `PlayerInteract -> HasItem -> SpawnItem` fonctionne en jeu.

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
| V1.1 | Éditeur Java, custom nodes `@MineNode`, API Mine Engine et bibliothèque `mineengine-runtime` | Un programmeur publie une méthode Java ; un designer l'utilise dans un graphe sans ouvrir le code |
| V1.2 | Éditeur de particules et preview temps réel | Un effet réglé dans la preview a le même rendu en jeu |
| V1.3 | Renderers visuels, surchargeables en Java | À définir à la fin de la V1.2 |
| V1.4 | Animation, import Blockbench | À définir |
| V1.5 | Entités et IA nodale | À définir |
| V1.6 | Système de plugins, backend Fabric | À définir |

---

## Décisions en attente

- [ ] Passer de .NET 9 à .NET 10 (LTS) : .NET 8 et 9 ne sont plus maintenus à partir du 10 novembre 2026. Il faut installer le SDK .NET 10 puis changer une ligne dans `Directory.Build.props`.
- [ ] Installer un JDK 21 (Temurin) pour les MDK Minecraft 1.21.1, au lieu de laisser Gradle le télécharger.
- [ ] Élargir les versions prises en charge par le générateur (autres versions de Forge et NeoForge, puis Fabric).
- [ ] Dépôt Git : secondaire pour le moment, à mettre en place plus tard.
- [ ] Mettre à jour le cahier des charges et le diagramme UML : bibliothèque de MDK, backends par loader, `IFileEmitter` placé dans le package Minecraft.
