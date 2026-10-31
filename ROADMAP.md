# Roadmap

| Version | Ajouts | État |
| --- | --- | --- |
| V0.1 | Prototype technique | F |
| V0.2 | Fondation de l'éditeur | F |
| V0.3 | Système d'assets, mobs et IA (simple) | F |
| V0.4 | Prototype nodal | F |
| V0.4.1 | Items complets, dossiers, onglets d'édition | F |
| V0.4.2 | Architecture façon MCreator, éditeur Java, thème sombre et bleu | F |
| V0.4.3 | Programmation en graphes : fonctions, procédures, classes, structures, interfaces, énumérations | F |
| V0.4.4 | Refonte graphique du projet | F |
| V0.4.5 | Ajouts de nouveaux icon + changement de la police d'écriture | F |
| V0.5 | Paramètrage plus poussé de l'interface | F |
| V0.5.1 | changement et importations de theme | F |
| V0.5.2 | changement et importation de langue | F |
| V0.5.3 | changement du layout via drag and drop (systeme Unreal) | F |
| V0.5.4 | ajout des reroute dans les nodes | F |
| V0.5.5 | sous level dans le menu de creation | F |
| V0.5.6 | Ajouts des raccourci clavier | F |
| V0.5.7 | Ajout de fonction d'ouverture avec un programme tièrce | F |
| V0.5.8 | Ajout d'un onglet ressource pour trier les assets | F |
| V0.5.9 | Ajout d'un onglet langue pour creer/edit les langue + import/export | F |
| V0.6 | Intégration du sous programme puppeteer (ai trees) | F |
| V0.6.1 | Ajouts des interactions avec click droit | F |
| V0.6.2 | creation de commands/creatives tabs/Keybind | F |
| V0.6.3 | creation d'enchantement/fluid/game rule/potion effect | F |
| V0.6.4 | ajout d'un systeme de debug de l'engine (mode debug dans les paramètres + rapports de crash) | F |
| V0.6.5 | ajout de node (voir node a ajouter.md : logique, boucles, maths, texte, monde, joueurs, inventaire, positions, événements) | F |
| V0.6.6 | ajout de node (pour faire des raycast + gestion de physique + Changement dynamiqeu de texture et modele 3D + traitement du son ; la caméra du joueur passe en V0.7.4) | F |
| V0.6.7 | Création de l'onglet Global | F |
| V0.6.8 | Creation de la fenetre de gestion d'assets pour les elements (double click sur la case de texture et afficher en grid toutes les textures disponnibles) | F |
| V0.6.9 | Creer une interaction click droit dans l'onglet dashboard pour (sortir du dossier/revenir à la racine/undo/redo/validate/execute/compile) | F |
| V0.6.10 | Fils et types : survol d'un fil + Retour arrière pour le couper, clic droit Rafraîchir les nœuds / Casser toutes les connexions, références de reroute, nœuds de cast + cast automatique (int vers float...), nouveaux types (char, long, float, byte, UUID, texte riche, identifiant, emplacement d'équipement, main, rareté, fluide, boîte AABB, source de dégâts, instance d'effet, NBT, monde, entité vivante) | F |
| V0.6.11 | Pins aimantées, message de statut de 3 s, navigateur d'assets (textures et modèles 3D, ceux de Minecraft compris), portées des variables globales (scopeGlobalVariable.md), blocs façon MCreator (rotation, eau, gravité, rendu, forme en boîtes, états personnalisés, redstone, feu, balise, échelle, génération de minerai) | F |
| V0.7.0 | Creation de plante/biome (fleur, plante haute, plante qui pousse, culture, parcelles dans le monde ; biomes de l'Overworld avec climat, couleurs et surface) + textures des blocs en croix | F |
| V0.7.1 | Ajout de l'onglet tags (tags.md : blocs, items, fonctions, peintures, dégâts, enchantements, entités, biomes, événements, structures, variantes, bannières, préréglages, potions, dialogues, fluides ; espaces de noms minecraft, mod, c, forge, neoforge, fabric) + nœuds « est dans le tag » | F |
| V0.7.2 | Integration de structure simple (arbre) | P |
| V0.7.3 | Ajout de l'onglet global variable (fait en V0.6.7) | F |
| V0.7.4 | Node de gestion de la caméra du joueur (domaine Caméra : orienter, tourner, regarder une position, voir depuis une entité, perspective F5, zoom, tremblement, inclinaison, interface F1, réinitialiser ; lacet, tangage, direction et position de la caméra) | F |
| V0.7.5 | Ajout du sous programme sparkle (particule) (Sparkle.md : systèmes à plusieurs émetteurs en nœuds sur un canevas façon Niagara, édition dans Propriétés, aperçu 3D carré et détachable, timeline lecture / pause / arrêt / à l'envers / image par image, événements de mort, nœuds « Jouer un système de particules ») | F |
| V0.7.6 | Ajouter des template pour les sparkle (émetteurs préconfigurés au clic droit : vide, rayon d'un point à un autre, fontaine, rebond, ruban) + événements de temps (une fois après x s ou à chaque intervalle) et de zone (entrée dans une sphère ou un cube) + paramètres utilisateur liés aux réglages, plusieurs textures par émetteur (au hasard ou animées), commentaires et infobulles, aimantation, copier / coller, aides de l'aperçu (axes, zones, rayons, vélocité, collisions) | F |
| V0.7.7 | Ajouter des nodes pour les sparkle (choix du système et du paramètre dans une liste, régler un paramètre utilisateur nombre / vecteur avant de jouer, jouer d'une position à une autre, arrêter les systèmes d'une entité ou autour d'une position) + inversion des contrôles de l'aperçu (Paramètres > Éditeurs) | F |
| V0.7.8 | Creation de pack (pouvoir creer directement un pack d'armure/tools, minerai etc avec le meme nom au départ par exemple) | P |
| V0.7.9 | Refonte de l'onglet mob (fenetre plus parametres) | F |
| V0.7.10 | Implémentation des damage type et drop table (en 3 étapes — 1 faite : élément Type de dégâts (messages de mort, épuisement, difficulté, son, propriétés en tags du jeu), nœud « Dégât d'un type », immunité des mobs aux types du projet ; 2 faite : élément Table de drop (type, pools et tirages, entrées item / tag / vide avec poids et quantité, chance, tué par un joueur, Toucher de soie, explosion, Butin, cuit si en feu, enchanté), écrite en loot table du jeu pour 1.20.1 et 1.21.1 ; 3 faite : un bloc lâche une table du projet (sous ses conditions d'outil), un mob lâche une table à la place de sa liste, nœud « Faire tomber une table de drop » ; plus tard : une table pour un coffre ou une structure) | F |
| V0.7.11 | Refonte de l'onglet block (block.md, en 3 étapes — étape 1 faite : onglets Visuel, Propriétés du bloc, Item du bloc, Redstone, Drops, États, Génération, cases qui activent les champs, outil précis et condition de drop, expérience fixe ou intervalle ; étape 2 faite : bases de bloc (escalier, dalle, barrière, portillon, muret, porte, trappe, vitre, bouton, plaque de pression) ; étape 3 faite : onglet Événements et déclencheurs, 17 nœuds d'événements du bloc, ticks réguliers ou au hasard ; plus tard : textures connectées, entité de bloc, table de drop) | F |
| V0.7.12 | Refonte de l'onglet item (item.md, en 3 étapes — étape 1 faite : onglets Visuel, Propriétés de l'item, Nourriture, Outil, Propriétés avancées ; texture des inventaires pour un modèle 3D, condition de brillance, item de réparation, récolte tous les blocs, reste dans la grille (lui-même, usé ou un autre item), durée, animation et item rendu de la nourriture, matériau d'outil personnalisé ; étape 2 faite : combustible et compost réglés par des fonctions, troc avec les piglins, onglet Item à distance (arc, arbalète, lancer, canne à pêche ; projectile, munitions, au relâchement, d'un coup ou en continu) ; étape 3 faite : onglet Événements et déclencheurs, 20 événements d'item (utilisé sur une créature, se casse, lâché, ramassé, nourriture, coups d'outil, charge et tir), qui donnent aussi l'autre entité et une valeur ; plus tard : poudre d'os, disque de musique, motif d'armure et de bannière, animation de tension de l'arc) | F |
| V0.7.13 | Méthodes de classe et affichage façon MCreator pour l'item (en 4 étapes — 1 faite : chaque item devient sa propre classe Java (package item), ModItems ne fait que l'enregistrer ; 2 faite : liaisons et événements deviennent des méthodes de cette classe, créées à la demande, en Java ou en nodal avec un graphe propre à l'item ; 3 faite : édition de ces méthodes (boutons + Java / + Graphe / Modifier / Supprimer, onglet de code du corps de la méthode, graphe de l'item créé avec ses paramètres, caché de l'explorateur et supprimé avec l'item) ; 4 faite : onglet item façon MCreator : sections encadrées, libellé et aide « ? » sur chaque ligne, lignes « Fixe / méthode » avec + et crayon, grille de cartes d'événements « faire : » ; ensuite : la même chose pour les blocs et les mobs) | F |
| V0.8.0 | Creation de Gui systeme de widget en xaml | |
| V0.8.1 | Quality of life improvement | |
| V0.8.2 | GUI dynamique + bind code | |
| V0.8.3 | GUI branch sur mob/item/Bock | |
| V0.8.5 | PostProcess sur la caméra du joueur | |
| V0.8.6 | Possibilité de travailler en graph et en node en même temps | |
| V0.8.7 | Creation de dimension | |
| V0.8.8 | Toucher à la génération du monde minecraft et des dimensions | |
| V0.8.9 | Structure complexe + jigsawBlock | |
| V0.9.0 | Systeme d'écoute pour détecter les mises à jours | |
| V0.9.1 | Systeme d'écoute pour changelog dans l'acceuil de l'app | |
| V0.9.4 | Élargir les versions prises en charge par le générateur (autres versions de Forge et NeoForge, puis Fabric) | |
| V0.9.5 | ajout d'un systeme de debug du projet | |

---

## V1 - Vx

| Version | Contenu principal | Critère de réussite |
| --- | --- | --- |
| V1.0 | Alpha exploitable | Aucun Bugs majeur et implémente sufissemment de fonctionnalités |
| V1.1 | Systeme de note/to do en markdown | Aucun Bugs majeur |
| V1.2 | Appliquer un outil de gestion de version via github pour les travaux en groupe | Un programmeur publie une méthode Java ; un autre l'utilise dans un graphe sans ouvrir le code et l'export/import de donnée fonctionne d'un pc à l'autre |
| V1.3 | Renderers visuels, surchargeables en Java via un sous programme | À définir |
| V1.4 | Système de plugins/librairies de code | À définir |

---

## Décisions en attente

- [ ] ModernWpf est utilisé en version 1.0.0-rc.1 (la branche 0.9 n'est plus maintenue) : passer à la 1.0 stable dès sa sortie.
- [ ] Passer de .NET 9 à .NET 10 (LTS) : .NET 8 et 9 ne sont plus maintenus à partir du 10 novembre 2026.
- [ ] Utilisation de Lucide Icons au lieu du pack de base de windows.

---

## Idées pour plus tard
