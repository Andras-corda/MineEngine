# 10. Comportements transverses

Ce document regroupe ce qui traverse tous les modules : annulation, enregistrement et sauvegardes, validation continue, journal, diagnostics, langues et gestion des erreurs.

## 1. Historique d'annulation

**Toute modification du projet passe par une commande annulable**, confiée à l'historique du projet. Aucune vue ne modifie un élément directement.

### La commande

Une commande a une description (affichée dans le menu : « Annuler : Modifier le nom affiché »), sait s'exécuter, s'annuler, et éventuellement absorber la commande suivante.

Sortes de commandes :

- **modification d'une propriété** : garde l'ancienne et la nouvelle valeur. Deux modifications successives de la même propriété du même objet, à moins de **2 secondes** d'intervalle, fusionnent en une seule étape (une saisie au clavier). Une commande « scellée » ne fusionne plus ;
- **commande composée** : plusieurs commandes appliquées et annulées ensemble (déplacer un dossier, importer plusieurs fichiers) ; elle annule dans l'ordre inverse ;
- **ajout et suppression d'un élément** : la suppression retient la position de l'élément dans le registre, pour le remettre à la même place ;
- **paramètres du projet** : une copie avant et une copie après ;
- **dossiers déclarés**, **fichiers verrouillés** : la liste avant et la liste après.

### L'historique

- Exécuter une commande vide la pile « rétablir » ; si la dernière commande l'absorbe, aucune nouvelle étape n'est créée.
- **Capacité : 500 étapes** ; au-delà, la plus ancienne est oubliée.
- Une commande ne peut pas en exécuter une autre pendant qu'elle est appliquée (erreur de programmation détectée).
- « Sceller » empêche la dernière commande d'absorber la suivante (fin d'une saisie, changement de champ).
- L'historique signale chaque changement ; le projet passe alors à « modifié ».
- L'historique est vidé à la fermeture du projet ; il ne survit pas à un redémarrage.

Les onglets de code ont leur propre historique, celui du texte.

## 2. Enregistrement

- **Ctrl+S** enregistre le projet (paramètres et tous les éléments, voir [06](06-formats-de-fichiers.md)) **et** tous les onglets de code modifiés.
- Après l'enregistrement, le projet n'est plus « modifié » ; la barre de statut affiche « Enregistré à HH:mm:ss ».
- Un échec d'écriture est signalé dans une boîte de dialogue et dans le journal ; le projet reste « modifié ».

### Sauvegarde automatique

- Toutes les N minutes (préférence, 5 par défaut, 0 la désactive, au plus 120), le projet est enregistré **s'il a été modifié** et si aucune compilation n'est en cours. Les onglets de code ne sont pas concernés.
- La barre de statut affiche « Enregistrement automatique à HH:mm:ss » et le journal « Projet enregistré automatiquement. »
- Changer la préférence relance le minuteur.

### Sauvegardes de secours

- À chaque enregistrement (manuel ou automatique), une sauvegarde de secours est créée si la plus récente date de plus de **10 minutes**.
- Ne sont gardées que les N plus récentes (préférence, 10 par défaut, de 1 à 100).
- Le menu Fichier ouvre le dossier des sauvegardes. La restauration est manuelle (décompresser l'archive).
- Un échec de sauvegarde de secours n'empêche pas l'enregistrement : il est seulement signalé dans le journal.

### Fermeture

Fermer un onglet modifié, le projet ou l'application demande : Enregistrer, Ne pas enregistrer, Annuler. Pendant une compilation, quitter propose d'arrêter le build. La date de dernière ouverture d'un projet est mise à jour dans la liste des projets connus quand il est ouvert ou créé.

## 3. Validation continue

- Après chaque modification du projet, une validation complète est relancée **400 ms après la dernière modification** (pour regrouper une saisie rapide).
- Ses diagnostics remplacent les résultats de validation précédents dans le panneau Diagnostics, la barre de statut et l'état de chaque élément du Dashboard (« OK », « 1 avertissement », « 2 erreurs »).
- Les graphes ouverts recalculent aussi leurs nœuds (une fonction appelée a changé, un type a été supprimé) et leurs problèmes.
- F7 valide immédiatement et affiche le panneau Diagnostics.

## 4. Diagnostics

Un diagnostic a :

| Champ | Description |
| --- | --- |
| Gravité | `Info`, `Warning`, `Error` |
| Message | une phrase, dans la langue de l'interface, qui explique le problème et si possible quoi faire |
| Source | texte lisible de l'origine (« Item 'magic_sword' », « src/.../X.java:12 »), ou rien |
| Élément | identifiant interne de l'élément concerné, pour y naviguer |
| Fichier, ligne | fichier du projet concerné et ligne, pour l'ouvrir au bon endroit |

Seule une **erreur** empêche de générer. Un **avertissement** signale un résultat probablement non voulu (le mod se génère quand même). Une **info** donne un conseil.

Les messages suivent un ton constant : phrases complètes, sans jargon de programmeur quand c'est évitable, avec les noms en français de l'interface entre guillemets (« Outil requis »), et la conséquence ou la correction après un deux-points.

## 5. Journal

- **Niveaux** : Debug, Info, Success, Warning, Error.
- **Destinations** : la console Sortie de l'éditeur (horodatée, colorée par niveau) et un fichier par jour (`logs/mineengine-AAAAMMJJ.log`), conservé 14 jours. Les deux reçoivent les mêmes messages.
- **Le journal accepte des écritures depuis plusieurs fils** (Gradle écrit depuis un autre fil). La console relaie les messages sur le fil de l'interface.
- **Un journal qui ne peut pas écrire ne fait jamais échouer l'application.**
- Messages notables : version et « prêt » au démarrage ; avertissement si aucun MDK n'est installé, sinon la liste des MDK ; ouverture, création, conversion d'un projet ; chaque étape de la compilation ; chaque ligne de Gradle.

## 6. Langues de l'interface

### Principe

- **Les textes sont écrits en français dans le code**, à l'endroit où ils servent. C'est la langue source.
- **Chaque autre langue est un fichier de traductions** qui associe chaque texte français à sa traduction. L'anglais est `Localization/en.json` (environ 1 500 textes).
- **L'anglais est la langue par défaut.** Le français est proposé dans les paramètres. Un texte sans traduction reste en français.
- **La langue est choisie une fois, au démarrage**, avant la création de toute fenêtre. Changer de langue dans les paramètres prend effet au prochain démarrage (un message le dit).
- **La culture suit la langue** (en-US ou fr-FR) : format des dates, des heures et des nombres.

### Dans le code

- Un texte simple : `Tr.T("Nouveau bloc")`.
- Un texte avec des valeurs : `Tr.F("Supprimer le dossier « {0} » ? Son contenu ({1}) remonte dans {2}.", nom, contenu, parent)`. La traduction garde les mêmes `{0}`, `{1}`, mais peut les placer ailleurs : c'est pour cela qu'une phrase est toujours traduite entière, jamais par morceaux.
- Dans les vues : une extension de balisage `{l:Tr 'Texte'}`.
- **Un même mot qui a deux sens doit avoir deux textes** : « Annuler » (bouton d'une fenêtre, « Cancel ») et « _Annuler » (menu Édition, « Undo »).
- **Ne sont pas traduits** : les identifiants, les noms techniques (Java, Gradle, JSON), et tout ce qui part dans le mod généré (code Java, `mods.toml`, fichiers de langue du jeu).

### Langues importées

- Une langue peut être ajoutée sans recompiler : un fichier `.json` (format dans [06](06-formats-de-fichiers.md)) importé depuis les paramètres est copié dans `%LOCALAPPDATA%\MineEngine\languages`.
- **Ordre de repli** : traduction de la langue importée, sinon l'anglais, sinon le texte français.
- Une traduction composite mal formée (accolade oubliée) ne fait pas planter l'interface : le texte source est affiché.
- Un fichier de langue devenu illisible au démarrage : l'interface reste en anglais.

### Contrôle automatique

Un test parcourt le code et les vues, extrait tous les textes passés au traducteur, et échoue si l'un d'eux n'a pas de traduction anglaise, ou si une traduction n'a pas les mêmes `{n}` que son texte source. Il vérifie aussi qu'il a bien trouvé plus de 1 000 textes (sinon c'est l'extraction qui est cassée).

### Choix pour la réécriture

Garder le français comme langue source n'est pas une obligation. Une réécriture peut passer à des clés neutres (`menu.file.save`) et à des fichiers par langue, à condition de garder les mêmes garanties : langue par défaut anglaise, contrôle automatique des traductions manquantes et des paramètres, phrases traduites entières. Voir [12](12-portage-rust.md).

## 7. Préférences de l'éditeur

| Préférence | Défaut | Valeurs |
| --- | --- | --- |
| Thème | identique au système | système, thèmes livrés, thèmes importés ; appliqué immédiatement en aperçu |
| Couleur d'accent | celle du thème | celle du thème ou une des sept proposées ; en aperçu |
| Taille de l'interface | 100 % | 80 % à 150 % |
| Langue | anglais | anglais, français, langues importées ; au prochain démarrage (redémarrage proposé) |
| Rouvrir le dernier projet | non | oui, non |
| Éditeur de code : taille du texte | 13 | 9 à 28 |
| Éditeur de code : retour à la ligne | non | oui, non |
| Graphes : grille affichée | oui | oui, non |
| Graphes : calage sur la grille | non | oui, non |
| Sauvegarde automatique | 5 minutes | 0 (désactivée) à 120 |
| Sauvegardes de secours conservées | 10 | 1 à 100 |

Les préférences sont enregistrées quand l'utilisateur valide la fenêtre ; annuler rétablit le thème et l'accent précédents. Les thèmes et langues importés ou supprimés le sont immédiatement (ce sont des fichiers). L'état de la barre latérale de l'accueil est enregistré dès qu'il change.

## 8. Gestion des erreurs

- **Les données de l'utilisateur ne doivent jamais être corrompues** : écriture atomique de tous les fichiers ; sauvegardes de secours ; aucune suppression de fichier que Mine Engine n'a pas créé ; suppression de fichiers par l'Explorateur vers la corbeille.
- **Un fichier illisible au démarrage** (préférences, projets connus, disposition) donne les valeurs par défaut, sans bloquer.
- **Un projet illisible** à l'ouverture : message qui cite le fichier et la propriété en cause ; le projet n'est pas ouvert.
- **Un projet d'une version plus récente** : refus explicite.
- **Une erreur inattendue** n'arrête pas l'application : elle est écrite dans le journal (avec sa pile d'appels) et affichée dans une boîte de dialogue (« Une erreur inattendue s'est produite : <message>. Le détail est affiché dans la console Output »).
- **Les erreurs attendues** (fichier introuvable, accès refusé, archive invalide, téléchargement échoué) sont des messages compréhensibles, jamais des traces techniques.
- **Une opération longue** (compilation, téléchargement) est annulable, et l'interface reste réactive pendant qu'elle tourne.

## 9. Statuts des fichiers du projet

L'éditeur connaît le statut de chaque fichier, d'après la liste des fichiers générés et la liste des fichiers verrouillés :

| Statut | Fichiers | Dans l'éditeur de code |
| --- | --- | --- |
| Écrit par l'utilisateur | tout ce que Mine Engine n'a pas généré | modifiable ; Mine Engine n'y touche jamais |
| Généré | listé dans `generated.json`, non verrouillé | lecture seule, bouton « Verrouiller pour modifier » |
| Verrouillé | listé dans les fichiers verrouillés | modifiable, bouton « Déverrouiller » ; plus réécrit par Mine Engine |
| Fichier projet | le `.mineengine` | lecture seule ; se modifie dans les paramètres du projet |
| Produit par Gradle ou le jeu | `build/`, `run/`, `.gradle/` | lecture seule |

Verrouiller et déverrouiller sont des commandes annulables. Le statut est recalculé après un verrouillage, une annulation ou une nouvelle génération.
