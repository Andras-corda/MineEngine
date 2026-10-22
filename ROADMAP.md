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
| V0.7.0 | Creation de plante/biome | |
| V0.7.1 | Ajout de l'onglet tags | |
| V0.7.2 | Integration de structure simple (arbre) | |
| V0.7.3 | Ajout de l'onglet global variable (fait en V0.6.7) | F |
| V0.7.4 | Node de gestion de la caméra du joueur | |
| V0.7.5 | Ajout du sous programme sparkle (particule) | |
| V0.7.6 | Ajouter des templace pour les sparkle (comme niagara system) | |
| V0.7.7 | Ajouter des events pour les sparkle (comme niagara system) | |
| V0.7.8 | Creation de pack (pouvoir creer directement un pack d'armure/tools, minerai etc avec le meme nom au départ par exemple) | |
| V0.8.0 | Creation de Gui/overlay | |
| V0.8.1 | Overlay dynamique + bind code | |
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

---

## Idées pour plus tard
