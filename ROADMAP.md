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
| V0.4.4 | Refonte graphique du projet | Finis |
| V0.4.5 | Ajouts de nouveaux icon + changement de la police d'écriture | Finis |
| V0.5 | Paramètrage plus poussé de l'interface | À faire |
| V0.5.1 | changement et importations de theme | À faire |
| V0.5.2 | changement et importation de langue | À faire |
| V0.5.3 | changement du layout via drag and drop (systeme Unreal) | À faire |
| V0.6 | Intégration du sous programme puppeteer (ai trees) | À faire |
| V0.6.1 | Bug fixe | À faire |


---

## V1 à ...

| Version | Contenu principal | Critère de réussite |
| --- | --- | --- |
| V1.0 | Alpha exploitable | Aucun Bugs majeur et implémente sufissemment de fonctionnalités |
| V1.1 | Copier la manière de faire d'unreal pour les blueprints | Un programmeur publie une méthode Java ; un autre l'utilise dans un graphe sans ouvrir le code |
| V1.1.5 | Appliquer un outil de gestion de version via github pour les travaux en groupe | Un programmeur publie une méthode Java ; un autre l'utilise dans un graphe sans ouvrir le code |
| V1.2 | Éditeur de particules et preview temps réel | Un effet réglé dans la preview a le même rendu en jeu |
| V1.2.4 | import et export de ces dit éffets | 2 personnes crée, s'envoie, moddifie et tout fonctionne |
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
