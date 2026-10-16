# Roadmap Mine Engine

Ce fichier suit l'avancement du projet version par version. Le détail de chaque version (objectifs, périmètre, choix techniques) est dans le cahier des charges.


| Version | Thème | État |
| --- | --- | --- |
| V0.1 | Prototype technique | Finis (critère de réussite validé) |
| V0.2 | Fondation de l'éditeur | Finis (critère de réussite validé) |
| V0.3 | Système d'assets, mobs et IA (simple) | En cours (Certaines choses manquante) |
| V0.4 | Prototype nodal | En cours (bugs) |
| V0.4.1 | Items complets, dossiers, onglets d'édition | En cours (interface des dossiers à finir) |
| V0.4.2 | Architecture façon MCreator, éditeur Java, thème sombre et bleu | Finis |
| V0.4.3 | Programmation en graphes : fonctions, procédures, classes, structures, interfaces, énumérations | Finis mais perfectible |
| V0.4.4 | Refonte graphique du projet | Finis |
| V0.4.5 | Ajouts de nouveaux icon + changement de la police d'écriture | Finis |
| V0.5 | Paramètrage plus poussé de l'interface | Finis |
| V0.5.1 | changement et importations de theme | Finis |
| V0.5.2 | changement et importation de langue | Finis |
| V0.5.3 | changement du layout via drag and drop (systeme Unreal) | Finis |
| V0.5.4 | ajout des reroute dans les nodes | Finis |
| V0.5.5 | sous level dans le menu de creation | À faire |
| V0.5.5 | Ajouts des raccourci clavier | À faire |
| V0.5.7 | Ajout de fonction d'ouverture avec un programme tièrce | À faire |
| V0.5.8 | Ajout d'un onglet ressource pour trier les assets | À faire |
| V0.5.9 | Ajout d'un onglet langue pour creer/edit les langue + import/export | À faire |
| V0.6 | Intégration du sous programme puppeteer (ai trees) | À faire |
| V0.6.1 | Ajouts des interactions avec click droit | À faire |
| V0.6.2 | creation de commands/creatives tabs/Keybind | À faire |
| V0.6.3 | creation d'enchantement/fluid/game rule/potion effect | À faire |
| V0.6.4 | ajout d'un systeme de debug de l'engine | À faire |
| V0.7.0 | Creation de plant/biome | À faire |
| V0.7.1 | Ajout de l'onglet tags | À faire |
| V0.7.2 | Integration de structure simple (arbre) | À faire |
| V0.7.3 | Ajout de l'onglet global variable | À faire |
| V0.7.5 | Ajout du sous programme sparkle (particule) | À faire |
| V0.8.0 | Creation de Gui/overlay | À faire |
| V0.8.5 | Creation de dimension | À faire |
| V0.9.0 | Creation de Gui/overlay | À faire |
| V0.9.5 | ajout d'un systeme de debug du projet | À faire |

---

## V1 à VX

| Version | Contenu principal | Critère de réussite |
| --- | --- | --- |
| V1.0 | Alpha exploitable | Aucun Bugs majeur et implémente sufissemment de fonctionnalités |
| V1.1 | Systeme de note/to do en markdown | Aucun Bugs majeur |
| V1.1 | Appliquer un outil de gestion de version via github pour les travaux en groupe | Un programmeur publie une méthode Java ; un autre l'utilise dans un graphe sans ouvrir le code et l'export/import de donnée fonctionne d'un pc à l'autre |
| V1.2 | Renderers visuels, surchargeables en Java via un sous programme | À définir |
| V1.3 | Système de plugins/librairies de code | À définir |

---

## Décisions en attente

- [ ] ModernWpf est utilisé en version 1.0.0-rc.1 (la branche 0.9 n'est plus maintenue) : passer à la 1.0 stable dès sa sortie.
- [ ] Passer de .NET 9 à .NET 10 (LTS) : .NET 8 et 9 ne sont plus maintenus à partir du 10 novembre 2026.
- [ ] Élargir les versions prises en charge par le générateur (autres versions de Forge et NeoForge, puis Fabric).

---

## Idées pour plus tard

- Gestion des structure et du jigsaw block.
- Un systeme de creation de note/to do list intégré.