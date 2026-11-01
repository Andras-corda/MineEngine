# Versioning

Liste de tous les ajouts et modifications apportés à Mine Engine, de la version la plus récente à la plus ancienne.

- Les versions V0.7.10 et V0.7.12 à V0.7.14 sont détaillées étape par étape.
- Les autres versions reprennent le contenu de ROADMAP.md.
- État : F = faite, P = en pause. Les versions pas encore commencées ne sont pas listées (voir ROADMAP.md).

## Sommaire

- V0.7.14 — Ajouts et modifications de qualityoflife.md — F
- V0.7.13 — Méthodes de classe et affichage façon MCreator pour l'item — F
- V0.7.12 — Refonte de l'onglet item — F
- V0.7.11 — Refonte de l'onglet block — F
- V0.7.10 — Implémentation des damage type et drop table — F
- V0.7.9 — Refonte de l'onglet mob — F
- V0.7.8 — Creation de pack — P (en pause)
- V0.7.7 — Ajouter des nodes pour les sparkle — F
- V0.7.6 — Ajouter des template pour les sparkle — F
- V0.7.5 — Ajout du sous programme sparkle — F
- V0.7.4 — Node de gestion de la caméra du joueur — F
- V0.7.3 — Ajout de l'onglet global variable — F
- V0.7.2 — Integration de structure simple — P (en pause)
- V0.7.1 — Ajout de l'onglet tags — F
- V0.7.0 — Creation de plante/biome — F
- V0.6.11 — Pins aimantées, message de statut de 3 s, navigateur d'assets — F
- V0.6.10 — Fils et types : survol d'un fil + Retour arrière pour le couper, clic droit Rafraîchir les nœuds / Casser toutes les connexions, références de reroute, nœuds de cast + cast automatique — F
- V0.6.9 — Creer une interaction click droit dans l'onglet dashboard pour — F
- V0.6.8 — Creation de la fenetre de gestion d'assets pour les elements — F
- V0.6.7 — Création de l'onglet Global — F
- V0.6.6 — ajout de node — F
- V0.6.5 — ajout de node — F
- V0.6.4 — ajout d'un systeme de debug de l'engine — F
- V0.6.3 — creation d'enchantement/fluid/game rule/potion effect — F
- V0.6.2 — creation de commands/creatives tabs/Keybind — F
- V0.6.1 — Ajouts des interactions avec click droit — F
- V0.6 — Intégration du sous programme puppeteer — F
- V0.5.9 — Ajout d'un onglet langue pour creer/edit les langue + import/export — F
- V0.5.8 — Ajout d'un onglet ressource pour trier les assets — F
- V0.5.7 — Ajout de fonction d'ouverture avec un programme tièrce — F
- V0.5.6 — Ajouts des raccourci clavier — F
- V0.5.5 — sous level dans le menu de creation — F
- V0.5.4 — ajout des reroute dans les nodes — F
- V0.5.3 — changement du layout via drag and drop — F
- V0.5.2 — changement et importation de langue — F
- V0.5.1 — changement et importations de theme — F
- V0.5 — Paramètrage plus poussé de l'interface — F
- V0.4.5 — Ajouts de nouveaux icon + changement de la police d'écriture — F
- V0.4.4 — Refonte graphique du projet — F
- V0.4.3 — Programmation en graphes : fonctions, procédures, classes, structures, interfaces, énumérations — F
- V0.4.2 — Architecture façon MCreator, éditeur Java, thème sombre et bleu — F
- V0.4.1 — Items complets, dossiers, onglets d'édition — F
- V0.4 — Prototype nodal — F
- V0.3 — Système d'assets, mobs et IA — F
- V0.2 — Fondation de l'éditeur — F
- V0.1 — Prototype technique — F

---

## V0.7.14 — Ajouts et modifications de qualityoflife.md — F

### Étape 1 : page d'accueil
- Barre latérale de l'accueil dans l'ordre demandé : Accueil, Mes projets, Paramètres, puis Nouveau, Ouvrir, Ajouter, puis la section ÉPINGLÉS.
- Projets épinglés : menu « Épingler / Désépingler » sur les cartes de projet ; les projets épinglés apparaissent dans la barre latérale. Le choix est enregistré dans la liste des projets connus (`KnownProjects`, champ `pinned`).
- Page d'accueil centrée : le nom du logiciel, puis le dernier projet ouvert.
- La carte « À propos » passe dans l'Aide.
- Onglet « Mes projets » en grille (cartes de 300 px de large qui passent à la ligne).
- Indicateur Verr. Maj : un badge apparaît dans la fenêtre principale quand la touche Verr. Maj est active.
- Test : un projet épinglé le reste après l'enregistrement.

### Étape 2 : paramètres et Sparkle
- Nouveau contrôle sélecteur de couleur (`ColorPicker`) : carré saturation / luminosité, barre de teinte, saisie du code `#RRGGBB`.
- Paramètres > Apparence : la couleur d'accent peut être n'importe quelle couleur (sélecteur « N'importe quelle couleur »).
- Paramètres > Éditeurs : couleurs des axes X, Y et Z du gizmo de l'aperçu Sparkle, avec un bouton Réinitialiser ; elles sont enregistrées dans les paramètres.
- Aperçu Sparkle : un clic sur un axe du gizmo tourne la caméra pour regarder dans le sens de cet axe.
- Sparkle : nouveau module « Générer un événement de collision » ; un émetteur peut recevoir l'événement « Collision » (choix dans « Recevoir ») et faire apparaître des particules là où une particule a heurté un bloc (éclaboussures, étincelles...).
  - L'événement n'est envoyé qu'une fois, quand la particule commence à toucher, avec une chance réglable.
  - Simulation de l'aperçu, IR et code Java généré (`COLLISION`, chance de collision) mis à jour.
  - Avertissements : émetteur qui génère des collisions sans le module Collision, émetteur qui reçoit les collisions d'un émetteur qui n'en génère pas.
- Tests : 5 modules d'événement, test de l'événement de collision, exemple « fusée » avec un événement de collision à 50 %.

### Étape 3 : ajout et suppression
- Menu Ajouter (bouton + de la barre d'outils et menu Assets > Ajouter) réorganisé en 9 catégories, construit depuis un seul catalogue (`NewElementCatalog`) :
  1. Contenu du jeu : Item, Bloc, Mob, Armure, Peinture (à venir).
  2. Monde : Biome, Dimension (à venir), Structure (à venir), Décoration (à venir), Fluide, Plante.
  3. Gameplay : Succès (à venir), Onglet créatif, Commande, Effet de statut, Enchantement, Projectile (à venir), Recette.
  4. Données du jeu : Règle de jeu, Touche, Table de drop, Type de dégâts, Échange de villageois (à venir), Métier de villageois (à venir).
  5. Particules (Sparkle).
  6. Arbres d'IA (Puppeteer).
  7. Programmation Java : Classe, Classe utilitaire, Énumération, Structure (record), Écouteur d'événements du jeu.
  8. Programmation visuelle : Graphe d'événements, Fonction, Procédure, Bibliothèque de fonctions, Classe, Énumération, Structure, Interface.
  9. Dossier.
- Les types pas encore disponibles sont grisés avec « (à venir) » et leur info-bulle reste visible.
- Nouvelle fenêtre de création (« Nouvel élément... ») : catégories à gauche, éléments en cartes au centre, nom saisi directement (l'identifiant en est tiré), choix d'un modèle, bouton Créer ; double-clic sur une carte pour créer. Un nom refusé garde la fenêtre ouverte et affiche la raison.
- Toutes les commandes d'ajout (menus, raccourcis Ajouter un item / bloc / mob / recette / arbre) passent par cette fenêtre.
- Modèles proposés :
  - Sparkle : Étincelles, Rayon, Fontaine, Rebond, Ruban (et émetteur vide).
  - Puppeteer (nouveau fichier `BehaviorTreeTemplates`) : Agressif, Promeneur, Craintif, Rancunier, Observateur, Arbre vide ; l'arbre de départ d'un nouvel arbre est le modèle Agressif.
  - Armure : Casque, Plastron, Jambières, Bottes (un item réglé sur la bonne pièce).
- Nouveaux modèles de classes Java : énumération et record.
- La création d'une classe Java, d'un graphe ou d'un élément accepte maintenant un nom donné à la création.
- Confirmation de suppression « Êtes-vous sûr ? » pour un élément, plusieurs éléments, un dossier (même vide) et un fichier de l'explorateur (corbeille) :
  - case « Ne plus demander jusqu'à la fermeture de Mine Engine » ;
  - réglage permanent dans Paramètres > Général > Suppression (« Demander une confirmation avant de supprimer ») ;
  - l'avertissement des éléments utilisés ailleurs reste toujours affiché.
- 68 textes français ajoutés.
- Tests : chaque modèle Puppeteer donne un arbre valide ; le build complet compile les classes Java énumération et record.

### Étape 4 : Dashboard, Global, Tags et Ressources
- Dashboard : nouveau sous-menu « Méthodes et graphes » au clic droit d'un élément :
  - pour un item, ouvre ses méthodes existantes et propose « Créer une méthode » pour chaque emplacement (lueur, combustion, compost, chacun des 20 événements), « En Java » ou « En graphe (programmation visuelle) » ;
  - pour tout élément, liste les graphes qui l'utilisent et les ouvre.
- Onglets Global et Tags : bouton « Fichier Java » qui génère le code puis ouvre la classe, et bouton `{ }` sur chaque ligne qui ouvre le fichier à la ligne de la variable ou du tag.
- Générateur :
  - chaque variable globale est rattachée à sa ligne dans `MineEngineGlobals` (source map) ;
  - nouvelle classe générée `MineEngineTags` : une constante `TagKey` par tag (blocs, items, entités, biomes, fluides, types de dégâts, enchantements, structures, événements de jeu), par exemple `stack.is(MineEngineTags.MOON_ORES)`, en 1.20.1 et 1.21.1 ; chaque ligne est rattachée à son tag.
- Ouvrir le code Java fonctionne pour n'importe quel élément, plus seulement l'élément sélectionné.
- Onglet Ressources :
  - un onglet par sorte en haut (Tout, Textures, Sons, Modèles 3D) avec leur nombre ;
  - colonne « Utilisé pour » : sous-catégories déduites des éléments qui utilisent chaque fichier et de son dossier (Items, Blocs, Mobs, Interface (GUI), Particules, Monde, Graphes, Autre, Inutilisé) ; une texture utilisée par un modèle 3D compte pour les éléments qui utilisent ce modèle ;
  - affichage en liste (colonnes) ou en tuiles (grille d'aperçus).
- 17 textes français ajoutés.
- Tests : la classe des tags et son rattachement aux lignes, les sous-catégories des ressources.

---

## V0.7.13 — Méthodes de classe et affichage façon MCreator pour l'item — F

### Étape 1 : une classe Java par item
- Chaque item devient sa propre classe Java (`item/<Nom>Item.java`) ; `ModItems` ne fait plus que l'enregistrer.
- Le matériau d'outil personnalisé est dans la classe (`NomItem.TIER`).
- Un nom qui entre en conflit avec une classe du jeu reçoit le préfixe « Mod ».

### Étape 2 : les méthodes et leur génération
- Modèle des méthodes d'élément (`ElementMethod`, `MethodSlot`, `ItemMethodSlots`) : condition de brillance, durée de combustion, condition de combustion, condition de compost, et une méthode par événement.
- Une méthode n'existe pas tant qu'on ne la crée pas ; son corps est du Java, ou un graphe nodal propre à l'item.
- Les graphes propres à un item (`GraphAsset.ElementId`, enregistré sous « element ») sont cachés de l'explorateur et supprimés avec l'item.
- Les méthodes sont générées en `public static` dans la classe de l'item.
- Signature des événements d'item : `(Player player, Level level, BlockPos position, Entity other, double amount)`.

### Étape 3 : édition des méthodes
- Boutons + Java / + Graphe / Modifier / Supprimer sur chaque méthode.
- Onglet de code pour le corps d'une méthode Java (`MethodDocumentViewModel`).
- Graphe de méthode créé avec ses paramètres et, pour une fonction, son nœud de retour.

### Étape 4 : affichage façon MCreator
- Onglet item en sections encadrées.
- Nouveau contrôle de ligne de propriété (`PropertyRow`) : libellé, aide « ? » et note sur chaque ligne, colonne des libellés de largeur fixe.
- Lignes « Fixe / méthode » avec + et crayon.
- Grille de cartes d'événements « faire : ».
- Menu ouvert au clic (`MenuOnClick`).

---

## V0.7.12 — Refonte de l'onglet item (item.md) — F

### Étape 1 : visuel, propriétés, nourriture, outil
- Onglets Visuel, Propriétés de l'item, Nourriture, Outil, Propriétés avancées.
- Texture des inventaires quand l'item a un modèle 3D.
- Condition de brillance (glint).
- Item de réparation ; récolte tous les blocs.
- Reste dans la grille de fabrication : lui-même, usé, ou un autre item.
- Nourriture : durée (en secondes, remplace « rapide »), animation d'utilisation, item rendu après consommation.
- Matériau d'outil personnalisé (`ItemTier.Custom`).

### Étape 2 : combustible, compost, piglins, item à distance
- Combustible et compost réglés par des fonctions (durée de combustion, condition de combustion, chance et condition de compost).
- Classe générée `ModItemHooks` : durée de combustion, compostables (Forge), condition de compost ; fichier `compostables.json` pour NeoForge.
- Troc avec les piglins (monnaie des piglins).
- Onglet Item à distance : arc, arbalète, lancer, canne à pêche ; projectile, munitions, tir au relâchement, d'un coup ou en continu.

### Étape 3 : événements et déclencheurs
- Onglet Événements et déclencheurs avec 20 événements d'item : utilisé sur une créature, se casse, lâché, ramassé, commence / arrête / continue d'être mangé, coups d'outil (balancement, coup donné), début de charge, chargé, charge en cours, tir, etc.
- Les événements donnent aussi l'autre entité et une valeur.
- Événements « item cassé » et « item ramassé » branchés sur les événements du loader.

---

## V0.7.11 — Refonte de l'onglet block — F

- Refonte de l'onglet block (block.md, en 3 étapes — étape 1 faite : onglets Visuel, Propriétés du bloc, Item du bloc, Redstone, Drops, États, Génération, cases qui activent les champs, outil précis et condition de drop, expérience fixe ou intervalle ; étape 2 faite : bases de bloc (escalier, dalle, barrière, portillon, muret, porte, trappe, vitre, bouton, plaque de pression) ; étape 3 faite : onglet Événements et déclencheurs, 17 nœuds d'événements du bloc, ticks réguliers ou au hasard ; plus tard : textures connectées, entité de bloc, table de drop)

---

## V0.7.10 — Types de dégâts et tables de drop — F

### Étape 1 : type de dégâts
- Nouvel élément Type de dégâts : message de mort, épuisement, mise à l'échelle avec la difficulté, effets (son), propriétés écrites en tags du jeu.
- Généré en JSON de données, tags `damage_type` du jeu et clés de traduction des messages de mort.
- Nœud « Dégât d'un type ».
- Immunité des mobs aux types de dégâts du projet.

### Étape 2 : table de drop
- Nouvel élément Table de drop : type, pools et tirages ; entrées item, tag ou vide avec poids et quantité ; chance, tué par un joueur, Toucher de soie, explosion, Butin, cuit si en feu, enchanté.
- Écrite en loot table du jeu pour la 1.20.1 (`looting_enchant`) et la 1.21.1 (`enchanted_count_increase`, prédicat `minecraft:enchantments`).

### Étape 3 : tables de drop liées aux éléments
- Un bloc peut lâcher une table du projet, sous ses conditions d'outil.
- Un mob peut lâcher une table à la place de sa liste de drops.
- Nœud « Faire tomber une table de drop ».
- Tests : exemples de dégâts et de tables de drop, et leurs tests.

---

## V0.7.9 — Refonte de l'onglet mob — F

- Refonte de l'onglet mob (fenetre plus parametres)

---

## V0.7.8 — Creation de pack — P (en pause)

- Creation de pack (pouvoir creer directement un pack d'armure/tools, minerai etc avec le meme nom au départ par exemple)

---

## V0.7.7 — Ajouter des nodes pour les sparkle — F

- Ajouter des nodes pour les sparkle (choix du système et du paramètre dans une liste, régler un paramètre utilisateur nombre / vecteur avant de jouer, jouer d'une position à une autre, arrêter les systèmes d'une entité ou autour d'une position) + inversion des contrôles de l'aperçu (Paramètres > Éditeurs)

---

## V0.7.6 — Ajouter des template pour les sparkle — F

- Ajouter des template pour les sparkle (émetteurs préconfigurés au clic droit : vide, rayon d'un point à un autre, fontaine, rebond, ruban) + événements de temps (une fois après x s ou à chaque intervalle) et de zone (entrée dans une sphère ou un cube) + paramètres utilisateur liés aux réglages, plusieurs textures par émetteur (au hasard ou animées), commentaires et infobulles, aimantation, copier / coller, aides de l'aperçu (axes, zones, rayons, vélocité, collisions)

---

## V0.7.5 — Ajout du sous programme sparkle — F

- Ajout du sous programme sparkle (particule) (Sparkle.md : systèmes à plusieurs émetteurs en nœuds sur un canevas façon Niagara, édition dans Propriétés, aperçu 3D carré et détachable, timeline lecture / pause / arrêt / à l'envers / image par image, événements de mort, nœuds « Jouer un système de particules »)

---

## V0.7.4 — Node de gestion de la caméra du joueur — F

- Node de gestion de la caméra du joueur (domaine Caméra : orienter, tourner, regarder une position, voir depuis une entité, perspective F5, zoom, tremblement, inclinaison, interface F1, réinitialiser ; lacet, tangage, direction et position de la caméra)

---

## V0.7.3 — Ajout de l'onglet global variable — F

- Ajout de l'onglet global variable (fait en V0.6.7)

---

## V0.7.2 — Integration de structure simple — P (en pause)

- Integration de structure simple (arbre)

---

## V0.7.1 — Ajout de l'onglet tags — F

- Ajout de l'onglet tags (tags.md : blocs, items, fonctions, peintures, dégâts, enchantements, entités, biomes, événements, structures, variantes, bannières, préréglages, potions, dialogues, fluides ; espaces de noms minecraft, mod, c, forge, neoforge, fabric) + nœuds « est dans le tag »

---

## V0.7.0 — Creation de plante/biome — F

- Creation de plante/biome (fleur, plante haute, plante qui pousse, culture, parcelles dans le monde ; biomes de l'Overworld avec climat, couleurs et surface) + textures des blocs en croix

---

## V0.6.11 — Pins aimantées, message de statut de 3 s, navigateur d'assets — F

- Pins aimantées, message de statut de 3 s, navigateur d'assets (textures et modèles 3D, ceux de Minecraft compris), portées des variables globales (scopeGlobalVariable.md), blocs façon MCreator (rotation, eau, gravité, rendu, forme en boîtes, états personnalisés, redstone, feu, balise, échelle, génération de minerai)

---

## V0.6.10 — Fils et types : survol d'un fil + Retour arrière pour le couper, clic droit Rafraîchir les nœuds / Casser toutes les connexions, références de reroute, nœuds de cast + cast automatique — F

- Fils et types : survol d'un fil + Retour arrière pour le couper, clic droit Rafraîchir les nœuds / Casser toutes les connexions, références de reroute, nœuds de cast + cast automatique (int vers float...), nouveaux types (char, long, float, byte, UUID, texte riche, identifiant, emplacement d'équipement, main, rareté, fluide, boîte AABB, source de dégâts, instance d'effet, NBT, monde, entité vivante)

---

## V0.6.9 — Creer une interaction click droit dans l'onglet dashboard pour — F

- Creer une interaction click droit dans l'onglet dashboard pour (sortir du dossier/revenir à la racine/undo/redo/validate/execute/compile)

---

## V0.6.8 — Creation de la fenetre de gestion d'assets pour les elements — F

- Creation de la fenetre de gestion d'assets pour les elements (double click sur la case de texture et afficher en grid toutes les textures disponnibles)

---

## V0.6.7 — Création de l'onglet Global — F

- Création de l'onglet Global

---

## V0.6.6 — ajout de node — F

- ajout de node (pour faire des raycast + gestion de physique + Changement dynamiqeu de texture et modele 3D + traitement du son ; la caméra du joueur passe en V0.7.4)

---

## V0.6.5 — ajout de node — F

- ajout de node (voir node a ajouter.md : logique, boucles, maths, texte, monde, joueurs, inventaire, positions, événements)

---

## V0.6.4 — ajout d'un systeme de debug de l'engine — F

- ajout d'un systeme de debug de l'engine (mode debug dans les paramètres + rapports de crash)

---

## V0.6.3 — creation d'enchantement/fluid/game rule/potion effect — F

- creation d'enchantement/fluid/game rule/potion effect

---

## V0.6.2 — creation de commands/creatives tabs/Keybind — F

- creation de commands/creatives tabs/Keybind

---

## V0.6.1 — Ajouts des interactions avec click droit — F

- Ajouts des interactions avec click droit

---

## V0.6 — Intégration du sous programme puppeteer — F

- Intégration du sous programme puppeteer (ai trees)

---

## V0.5.9 — Ajout d'un onglet langue pour creer/edit les langue + import/export — F

- Ajout d'un onglet langue pour creer/edit les langue + import/export

---

## V0.5.8 — Ajout d'un onglet ressource pour trier les assets — F

- Ajout d'un onglet ressource pour trier les assets

---

## V0.5.7 — Ajout de fonction d'ouverture avec un programme tièrce — F

- Ajout de fonction d'ouverture avec un programme tièrce

---

## V0.5.6 — Ajouts des raccourci clavier — F

- Ajouts des raccourci clavier

---

## V0.5.5 — sous level dans le menu de creation — F

- sous level dans le menu de creation

---

## V0.5.4 — ajout des reroute dans les nodes — F

- ajout des reroute dans les nodes

---

## V0.5.3 — changement du layout via drag and drop — F

- changement du layout via drag and drop (systeme Unreal)

---

## V0.5.2 — changement et importation de langue — F

- changement et importation de langue

---

## V0.5.1 — changement et importations de theme — F

- changement et importations de theme

---

## V0.5 — Paramètrage plus poussé de l'interface — F

- Paramètrage plus poussé de l'interface

---

## V0.4.5 — Ajouts de nouveaux icon + changement de la police d'écriture — F

- Ajouts de nouveaux icon + changement de la police d'écriture

---

## V0.4.4 — Refonte graphique du projet — F

- Refonte graphique du projet

---

## V0.4.3 — Programmation en graphes : fonctions, procédures, classes, structures, interfaces, énumérations — F

- Programmation en graphes : fonctions, procédures, classes, structures, interfaces, énumérations

---

## V0.4.2 — Architecture façon MCreator, éditeur Java, thème sombre et bleu — F

- Architecture façon MCreator, éditeur Java, thème sombre et bleu

---

## V0.4.1 — Items complets, dossiers, onglets d'édition — F

- Items complets, dossiers, onglets d'édition

---

## V0.4 — Prototype nodal — F

- Prototype nodal

---

## V0.3 — Système d'assets, mobs et IA — F

- Système d'assets, mobs et IA (simple)

---

## V0.2 — Fondation de l'éditeur — F

- Fondation de l'éditeur

---

## V0.1 — Prototype technique — F

- Prototype technique
