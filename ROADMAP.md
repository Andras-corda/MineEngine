# Roadmap Mine Engine

Ce fichier suit l'avancement du projet version par version. Le détail de chaque version (objectifs, périmètre, choix techniques) est dans le cahier des charges.


| Version | Thème | État |
| --- | --- | --- |
| V0.1 | Prototype technique | Finis (critère de réussite validé) |
| V0.2 | Fondation de l'éditeur | Finis (critère de réussite validé) |
| V0.3 | Système d'assets, mobs et IA (simple) | En cours (Certaines choses manquante) |
| V0.4 | Prototype nodal | En cours (bugs) |
| V0.4.1 | Items complets, dossiers, onglets d'édition | En cours (interface des dossiers à finir) |
| V0.4.2 | Architecture façon MCreator, éditeur Java, thème sombre et bleu | En cours |
| V0.4.3 | Programmation en graphes : fonctions, procédures, classes, structures, interfaces, énumérations | En cours |
| V0.5 | Paramètrage plus poussé de l'interface | À faire |
| V0.6 | Intégration du sous programme puppeteer (ai trees) | À faire |
| V1.0 | Alpha exploitable | À faire |
| V1.1 | Code et custom nodes | À faire |
| V1.2 | Particules | À faire |
| V1.3 | Intégration du sous programme des renderer | À faire |
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
- [x] Valider le critère de réussite à la main, notamment sur un vrai build en erreur (la remontée `javac` n'est testée que par des tests automatiques)
- [x] Vérifier en jeu les nouveaux items et blocs (nourriture, minerai qui laisse un item, bûche, fleur, bloc lumineux)

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
- [x] Couleur d'accent fixe (lilas, devenue bleue en V0.4.2), cartes et barre de statut harmonisées, menu Build renommé Compilation, version affichée depuis `Directory.Build.props`
- [x] Espace de travail refait : barre d'outils supprimée ; Explorateur façon VS Code à gauche (arborescence, recherche, création, renommage, corbeille, structure du fichier) ; onglets de code au centre (AvalonEdit, édition, lecture seule pour les fichiers générés) ; Dashboard en onglet permanent (liste ou vignettes, filtres, état, utilisations) ; panneau Détails / Inspecteur à droite ; barre de statut (curseur, encodage, langage)
- [x] Interface responsive : accueil sur une colonne, panneaux latéraux et du bas réduits, colonnes secondaires du Dashboard masquées en fenêtre étroite
- [x] Style de l'accueil appliqué partout : un seul dictionnaire de styles (`Themes/BaseStyles.xaml`), palette neutre et lilas sans bleu, panneaux AvalonDock rhabillés (thème `MineEngineDockTheme` construit depuis la palette), sélection des listes, onglets soulignés et fenêtres de dialogue assortis

Déplacé :
- Asset Graph : avec le node editor en V0.4.
- Asset Script : remplacé en V0.4.2 par les classes Java de l'utilisateur dans `src/main/java`.

Reste à faire pour clore la V0.3 :
- [ ] Valider le critère de réussite à la main dans l'éditeur, puis en jeu
- [ ] Vérifier les mobs en jeu (IA, apparition naturelle, reproduction, œufs) et les recettes

---

## V0.4 - Prototype nodal

Objectif : un premier node editor, volontairement réduit.

**Critère de réussite :** le graphe `PlayerInteract -> HasItem -> SpawnItem` fonctionne en jeu.

- [x] Asset Graph (déplacé de la V0.3) : graphe immuable (nœuds, fils), annulable comme le reste, rangé dans `Content/Graphs`
- [x] Node editor : créer, déplacer, connecter, supprimer, dupliquer des nodes ; sélection au rectangle, déplacement de la vue et zoom, menu d'ajout avec recherche, fil lâché dans le vide qui crée un node déjà branché, valeurs réglées sur les nodes
- [x] Catégories Event (4), Condition (5), Action (8), Variable (11)
- [x] Pins d'exécution et pins de données typés (booléen, nombre, texte, joueur, position, item, bloc, son), fils colorés par type, conversion nombre vers texte
- [x] Sauvegarde et chargement des graphes, références par `guid` aux items, blocs et sons du projet (« Utilisé par », renommage sans casse)
- [x] Validation : entrées obligatoires, valeurs saisies, nodes jamais exécutés, boucles, valeurs prises dans un autre événement, annulation d'un événement qui ne s'annule pas ; nodes en erreur entourés dans l'éditeur, problèmes dans Détails et Diagnostics
- [x] Partie comportement du Mod IR (`IRBehavior`) et `GraphCompiler`
- [x] Génération Java des événements pour Forge 1.20.1, Forge 1.21.1 et NeoForge 1.21.1, vérifiée par un build Gradle réel ; les erreurs `javac` remontent au graphe

Choix : l'éditeur nodal est écrit en WPF (canevas, fils en courbes de Bézier) plutôt qu'avec Nodify, en s'inspirant des ébauches NodalCraft (couleurs par type, menu d'ajout, valeurs en ligne).

Reste à faire pour clore la V0.4 :
- [ ] Valider le critère de réussite en jeu : clic droit sur un bloc avec trois rubis, un diamant apparaît

---

## V0.4.1 - Items complets, dossiers et onglets d'édition

- [x] Types d'items : simple, épée, pioche, hache, pelle, houe (matériau, dégâts, vitesse d'attaque), casque, plastron, jambières, bottes (matériau d'armure) ; constructeurs propres à 1.20.1 et 1.21.1
- [x] Nourriture : se mange vite, effets à la consommation (27 effets, durée, niveau, chance)
- [x] Données : item rendu après fabrication, non réparable, combustible (durée), tags d'items, tenu comme un outil
- [x] Onglet Événements : clic droit, utilisé sur un bloc, frappe une créature, bloc cassé, mangé, fabriqué, tenu en main ; chaque événement se relie à un graphe (créé au besoin) qui s'ouvre sur le nœud
- [x] Dossiers et sous-dossiers logiques (enregistrés dans le projet, annulables), sans effet sur les fichiers
- [x] Création et modification d'un élément dans un onglet d'édition (plus dans l'inspecteur)
- [x] Génération vérifiée par un build Gradle réel sur Forge 1.20.1, Forge 1.21.1 et NeoForge 1.21.1

- [x] Dossiers dans le Dashboard (liste et vignettes) : fil d'Ariane, dossiers avant les éléments, double-clic pour entrer, Retour arrière pour remonter, création, renommage, suppression (le contenu remonte), « Déplacer vers » et glisser-déposer sur un dossier ou le fil d'Ariane ; un nouvel élément est rangé dans le dossier ouvert ; la recherche porte sur tous les dossiers et montre le dossier de chaque élément

Reste à faire :
- [ ] Panneau Détails réduit à un résumé avec « Ouvrir l'éditeur »

Reporté (liste de référence des items) :
- Enchantements (enchantabilité, enchantements autorisés), matériaux d'armure et d'outil personnalisés
- Rendus et modèles 3D personnalisés (V1.3), capabilities, échanges des villageois, composteur, distributeur
- Lier un événement à du code Java plutôt qu'à un graphe : avec les custom nodes en V1.1

---

## V0.4.2 - Architecture façon MCreator, éditeur Java, thème sombre et bleu

Objectif : une application plus fluide, où le code Java a sa place à côté des éléments, comme dans MCreator.

- [x] Le dossier du projet est le projet Gradle du mod (comme un workspace MCreator) : fichier `<Nom>.mineengine`, éléments dans `elements/`, code dans `src/main/java`, ressources dans `src/main/resources`, `.jar` dans `build/libs`, mondes de test dans `run/`
- [x] Conversion automatique des anciens projets à l'ouverture (archive de l'ancien projet dans `.mineengine/backups`) ; les classes de `Scripts/` rejoignent le dossier de leur package
- [x] Liste des fichiers générés (`.mineengine/generated.json`) : seuls les fichiers générés qui ne sont plus produits sont supprimés, le code écrit à la main n'est jamais touché ; un fichier identique n'est pas réécrit
- [x] Code verrouillé : un fichier généré (Java, ressource, `build.gradle`) se verrouille pour être modifié à la main ; Mine Engine ne le réécrit plus ; annulable
- [x] Classes Java de l'utilisateur : écouteur d'événements du jeu (annotation propre à Forge ou NeoForge), classe utilitaire, classe vide ; compilées avec le mod (vérifié par un build Gradle réel sur les trois versions)
- [x] Éditeur Java : barre d'outils (Enregistrer, Rechercher, Formater), blocs repliables, indentation automatique, fermeture des parenthèses et guillemets, auto-complétion avec ajout des imports, formatage (Ctrl+Maj+F), recherche et remplacement, erreurs `javac` soulignées dans le code et pointées dans la marge
- [x] Erreurs de compilation : double-clic dans Diagnostics ouvre l'élément concerné ou le fichier Java à la ligne fautive
- [x] Thème sombre et bleu : palette gris bleuté, accent bleu (#2F7FD8), onglet actif plein, coloration du code façon VS Code ; le thème clair suit le même accent

- [x] Boutons de disposition dans la barre de titre, comme VS Code : personnaliser (mode concentration, tout afficher, réinitialiser), Explorateur (Ctrl+B), Sortie et Diagnostics (Ctrl+J), Détails (Ctrl+Alt+B)

Reste à faire :
- [ ] « Voir le code Java » depuis un élément du Dashboard, et verrouillage par élément (tous ses fichiers)
- [ ] Aller à la ligne (Ctrl+G), ouverture rapide de fichiers et d'éléments (Ctrl+P)

---

## V0.4.3 - Programmation en graphes

Objectif : les graphes ne décrivent plus seulement des réactions aux événements, mais un vrai programme, comme les Blueprints.

- [x] À la création d'un graphe, choix de ce qu'il définit : graphe d'événements, fonction, procédure, classe, structure, interface ou énumération
- [x] Panneau « Définition » : nom, accessibilité (public, protégé, privé), paramètres, valeurs renvoyées (plusieurs possibles), variables locales ou globales, fonction pure, méthode statique
- [x] Classes : champs avec accessibilité, constructeur en nœuds (entrée avec ses paramètres, « Constructeur parent » en premier), classe parente, interfaces implémentées, méthodes et types intégrés (imbriqués)
- [x] Structures (champs), interfaces (méthodes à fournir), énumérations (valeurs)
- [x] Nœuds tirés des définitions : appeler (fonction, procédure, méthode, méthode d'interface), créer un objet, lire et modifier un champ ou une variable, valeur et comparaison d'énumération, « Moi », retour ; les nœuds suivent les changements de signature
- [x] Visibilité : public partout ; privé hors classe limité au dossier du Dashboard ; membre privé limité à sa classe, protégé aussi aux classes héritières ; un type privé intégré n'est utilisable qu'à l'endroit où il est intégré
- [x] Héritage : champs et méthodes hérités, redéfinition (même signature), un objet d'une classe héritière alimente une entrée de sa parente ou de ses interfaces ; erreurs pour les boucles d'héritage, méthodes d'interface manquantes, signatures différentes, constructeur parent oublié
- [x] Nœuds de calcul : addition, soustraction, multiplication, plus petit que
- [x] Génération Java dans le package `code` : classes, `extends`, `implements`, `@Override`, `record` pour plusieurs valeurs renvoyées, énumérations et types imbriqués ; le monde (`Level`) est passé à chaque méthode ; vérifiée par un build Gradle réel sur Forge 1.20.1, Forge 1.21.1 et NeoForge 1.21.1
- [x] Retour arrière supprime les nœuds sélectionnés (sauf l'entrée)

Reste à faire :
- [ ] Boucles (pour, tant que), listes, conversion entre types (« est un », transtypage), nombres décimaux
- [ ] Classes et méthodes abstraites, surcharge (plusieurs méthodes de même nom)
- [ ] Appeler une méthode Java écrite à la main depuis un graphe (custom nodes, V1.1)

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
| V1.1 | Custom nodes `@MineNode` (l'éditeur Java et le code de l'utilisateur sont arrivés en V0.4.2), API Mine Engine et bibliothèque `mineengine-runtime` | Un programmeur publie une méthode Java ; un designer l'utilise dans un graphe sans ouvrir le code |
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
