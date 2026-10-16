# 09. Interface utilisateur

L'interface reprend trois modèles : **VS Code** pour la structure générale (explorateur à gauche, onglets au centre, panneau du bas, boutons de disposition dans la barre de titre, raccourcis), **Unreal Engine** pour l'éditeur nodal et le panneau Détails, **MCreator** pour les éditeurs d'éléments en onglets thématiques. Elle doit rester calme : gris neutres, un seul accent bleu, icônes au trait, textes courts.

Ce document décrit ce que l'utilisateur voit et peut faire. Il ne prescrit pas de bibliothèque graphique (voir [12](12-portage-rust.md)).

## 1. Fenêtre principale

De haut en bas :

1. **Barre de titre** (34 px, couleur des barres latérales), dessinée par l'application :
   - à gauche, le cube du logo (bleu) et « Mine Engine » ;
   - au centre, le titre : « <nom du projet> — Mine Engine », avec une étoile si le projet ou un onglet a des modifications non enregistrées ; « Mine Engine — Sélection de projet » sans projet ;
   - à droite, quand un projet est ouvert, quatre boutons de disposition (personnaliser, Explorateur, panneau du bas, Détails), puis les boutons système (réduire, agrandir, fermer).
2. **Barre de menus** (section 3).
3. **Barre d'outils** (projet ouvert seulement), sous les menus :
   - à gauche : bouton bleu **+** (menu : item, bloc, mob, recette ; les sept sortes de graphes ; importer des textures, des sons), puis, pour l'élément sélectionné : **dupliquer** (Ctrl+D ; copie `<id>_copy` dans le même dossier, nom suivi de « (copie) » ; pas pour une texture ni un son), **supprimer**, **éditer** (ouvre son onglet), **ouvrir le graphe** (graphes seulement), **voir le code Java** (génère le code du mod sans compiler, puis ouvre le premier fichier Java produit par l'élément, à sa ligne ; un élément sans Java le dit dans la barre de statut) ; puis **nouveau dossier** dans le dossier ouvert du Dashboard ;
   - à droite : **recherche** des éléments du mod (Ctrl+F hors d'un éditeur de code ; taper affiche le Dashboard), bascule **Liste** / **Vignettes**, **Valider** (F7), **Compiler**, **Arrêter** (pendant un build), **Lancer** (bouton bleu, F5).
4. **Espace de travail** : panneaux ancrables quand un projet est ouvert, accueil sinon.
5. **Barre de statut** : point d'état et nom du projet, message d'état (« Prêt », « Enregistré à 14:02:11 », « Build en cours... »), résumé des problèmes (« 0 erreur · 2 avertissements », cliquable vers les Diagnostics), boutons Générer et Lancer, position du curseur et langage quand un onglet de code est actif, version de l'application.

### Panneaux

| Panneau | Place par défaut | Contenu |
| --- | --- | --- |
| Explorateur | gauche (270 px, minimum 180) | arborescence des fichiers, structure du fichier ouvert |
| Onglets | centre | le Dashboard (onglet permanent), puis un onglet par élément, graphe ou fichier ouvert |
| Sortie | bas (220 px, minimum 90) | journal |
| Diagnostics | bas, empilé avec Sortie | problèmes |
| Détails / Inspecteur | droite (380 px, minimum 240) | la cible sélectionnée et ses propriétés |

Les panneaux peuvent être déplacés, empilés, détachés en fenêtres flottantes, masqués. Pendant qu'on glisse un panneau, les **guides de dépôt** (comme dans Unreal) prennent les couleurs du thème : boutons couleur carte, flèches couleur texte, centre couleur barre latérale, et la zone où le panneau va se poser est remplie de l'accent en transparence avec un trait d'accent. **La disposition est enregistrée à la fermeture et restaurée au démarrage.** Si la disposition enregistrée ne contient pas un panneau (nouvelle version), la disposition par défaut est utilisée. Une fenêtre flottante vidée de ses panneaux est retirée, pour que ses panneaux reviennent dans la fenêtre principale quand on les rouvre. « Réinitialiser la disposition » remet celle d'origine.

Les onglets n'ont pas de menu déroulant de liste ; Ctrl+Tab permet de passer de l'un à l'autre.

### Boutons de disposition (barre de titre)

| Bouton | Action | Raccourci |
| --- | --- | --- |
| Personnaliser | menu : cases Explorateur, Sortie et diagnostics, Détails ; Mode concentration (masque tous les panneaux) ; Tout afficher ; Réinitialiser la disposition | |
| Explorateur | affiche ou masque l'Explorateur | Ctrl+B |
| Panneau du bas | affiche ou masque Sortie et Diagnostics ensemble | Ctrl+J |
| Détails | affiche ou masque le panneau Détails | Ctrl+Alt+B |

Un bouton dont le panneau est affiché a une icône plus claire. L'état suit aussi les panneaux fermés, déplacés ou rouverts par d'autres moyens.

## 2. Accueil

Affiché quand aucun projet n'est ouvert, sans en-tête d'onglet.

**Barre latérale** (220 px, réductible à 58 px) :

- en haut, un bouton menu à trois traits (« Réduire la barre latérale » / « Agrandir la barre latérale ») et la légende « ESPACE DE TRAVAIL » ;
- les actions de projet : **Nouveau projet...** (bouton bleu), **Ouvrir un projet...**, **Ajouter un projet...** (ajoute un projet existant à la liste sans l'ouvrir) ;
- les pages : **Accueil**, **Mes projets**, **Paramètres** (ouvre la fenêtre des paramètres) ;
- en bas : **Gérer les MDK**, **Aide et documentation**, puis « Mine Engine » et la version.

Réduite, la barre ne garde que les icônes (avec une info-bulle). Elle se réduit d'office quand la fenêtre fait moins de 760 px de large, et son bouton est alors désactivé.

**Pages** :

- **Accueil** : vide pour l'instant (réservée à un futur tableau de bord).
- **Mes projets** : titre et nombre de projets, tri (nom du projet, ouverture récente, version de Minecraft), recherche, onglets « Tous les projets » et « Récents » (les 5 plus récemment ouverts), puis la liste. Chaque carte montre l'icône du projet (sa première texture, sinon un cube), le nom, un badge loader et version avec l'icône du loader, la description, le chemin (avec `~` pour le dossier personnel), la date de dernière ouverture (« Jamais ouvert », « Ouvert aujourd'hui à 18:22 », « Ouvert hier », « Ouvert il y a N jours », date longue au-delà de 30 jours) et un résumé (« monmod · 13 items · 5 blocs · 3 mobs »). Un projet dont le dossier a disparu est marqué « INTROUVABLE ». Double-clic : ouvrir. Clic droit : ouvrir, ouvrir le dossier, retirer de la liste (avec confirmation ; le dossier n'est pas supprimé). La molette fait défiler toute la page.
- **Aide et documentation** : démarrage rapide en cinq étapes, raccourcis clavier, emplacement des fichiers.

## 3. Menus

| Menu | Entrées |
| --- | --- |
| Mine Engine | Dépôt GitHub (ouvre le dépôt dans le navigateur) ; Site web (grisé tant que le site n'existe pas) |
| Fichier | Nouveau projet... (Ctrl+N), Ouvrir un projet... (Ctrl+O), Enregistrer tout (Ctrl+S), Enregistrer le fichier, Fermer l'onglet (Ctrl+W), Fermer le projet (retour à l'accueil) ; Quitter |
| Édition | Annuler (Ctrl+Z, avec la description de l'action : « Annuler : Modifier le nom affiché »), Rétablir (Ctrl+Y) ; Éditer, Dupliquer (Ctrl+D), Supprimer l'élément sélectionné ; Ouvrir le graphe sélectionné, Voir le code Java, Voir le fichier de l'élément |
| Espace de travail | Paramètres du projet... ; Ouvrir le dossier du projet, Ouvrir le dossier des sauvegardes ; Nouveau dossier, Nouvelle classe Java (écouteur d'événements du jeu, classe utilitaire, classe vide) ; Valider le projet (F7) |
| Éléments | Ajouter un item, un bloc, un mob, une recette ; Ajouter un graphe (événements, fonction, procédure, classe, structure, interface, énumération) ; Importer des textures (PNG), des sons (OGG), un élément (`.asset.json` exporté d'un autre projet : nouvel identifiant interne, identifiant libre, rangé dans le dossier ouvert ; pas pour une texture ni un son) ; Exporter l'élément sélectionné (son `.asset.json`, ou l'image ou le son d'une texture ou d'un son) |
| Outils | Gestionnaire de MDK... ; Paramètres... (Ctrl+,) |
| Compilation et lancement | Compiler le mod (Ctrl+Maj+B), Exporter le mod (.jar)... (compile puis copie le `.jar` à l'endroit choisi), Lancer Minecraft (F5), Recharger Gradle (régénère le projet puis `gradlew --refresh-dependencies dependencies --configuration compileClasspath`, sans compiler) ; Arrêter ; Ouvrir le dossier des mods produits |
| Fenêtre | Dashboard, Explorateur (Ctrl+B), Détails / Inspecteur (Ctrl+Alt+B), Sortie, Diagnostics ; Mode concentration, Tout afficher, Réinitialiser la disposition |

Les entrées de création ont l'icône de la sorte d'élément. Les commandes de modification sont désactivées pendant une compilation.

## 4. Dashboard

L'onglet permanent qui liste les éléments du mod.

Pas d'en-tête : le Dashboard commence directement par la liste. Les actions (ajout, recherche, mode d'affichage, compilation) sont dans la barre d'outils de la fenêtre ; le nom du projet et l'état des problèmes sont dans la barre de titre et la barre de statut. On entre dans un dossier par double-clic et on remonte avec Retour arrière ; un élément se range en le déposant sur un dossier.

**Contenu** : les dossiers du dossier ouvert d'abord (nom, contenu « 10 élément(s), 2 dossier(s) », badge DOSSIER), puis les éléments. En liste, colonnes ÉLÉMENT (icône ou texture, nom, identifiant complet), ÉTAT (« OK », « 1 avertissement », « 2 erreurs », avec une pastille de couleur), UTILISÉ PAR (nombre de liens entrants), TYPE (badge). En vignettes, une grille de cartes de 120 px.

- Sans recherche, seul le dossier ouvert est montré ; **une recherche porte sur tous les dossiers** et affiche le dossier de chaque résultat.
- Double-clic sur un dossier : y entrer. Double-clic sur un élément : ouvrir son éditeur. Retour arrière : dossier parent.
- **Glisser un élément** sur un dossier ou un segment du fil d'Ariane : le déplacer.
- Clic droit sur un élément : ouvrir, afficher dans Détails, voir le fichier, déplacer vers (liste des dossiers), supprimer. Sur un dossier : ouvrir, renommer, supprimer le dossier (son contenu remonte d'un niveau, après confirmation s'il n'est pas vide).
- Un nouvel élément est rangé dans le dossier ouvert.
- Dossier vide : « Ce dossier est vide » et une explication. Mod vide : « Le mod est vide » et une explication.

**Supprimer un élément utilisé** demande confirmation en listant ce qui l'utilise ; les liens cassés seront signalés dans Diagnostics, et Ctrl+Z annule.

## 5. Éditeur d'un élément

Chaque élément s'ouvre dans son propre onglet. En-tête : icône (texture ou icône de la sorte), nom, badge de sorte, identifiant complet, état. Puis un groupe « Général » (nom en jeu, identifiant avec sa règle, identifiant complet, liste « Utilisé par » cliquable), puis des onglets thématiques :

| Élément | Onglets |
| --- | --- |
| Item | Visuel (texture, tenu comme un outil, effet brillant), Type (simple, outils, armes, armures ; matériau ; dégâts et vitesse), Propriétés (pile, rareté, résistance au feu, durabilité, info-bulle, onglets créatifs), Nourriture (comestible, valeurs, effets), Données (combustible, item rendu, non réparable, tags), Événements (les sept événements d'item, chacun relié ou non à un graphe, avec un bouton pour l'ajouter) |
| Bloc | Visuel (forme, textures par emplacement, transparence, lumière), Propriétés (dureté, résistance, incassable, sons, glissance, vitesse, saut), Récolte (outil, niveau, outil requis, ce qu'il laisse, quantités, expérience), Item (forme item et ses propriétés d'inventaire) |
| Mob | Apparence (modèle, texture, taille personnalisée avec la taille d'origine en rappel), Attributs (famille, vie, armure, vitesse, dégâts, portée, recul, expérience, feu, soleil, persistance), IA (liste ordonnée, monter, descendre, retirer, ajouter ; réglages de chaque comportement ; item de reproduction), Apparition (œuf et ses couleurs, apparition naturelle, biomes, fréquence, groupes), Sons et butin |
| Recette | sorte, grille 3 × 3 ou ingrédient du four, résultat et quantité, expérience et durée de cuisson |
| Texture | aperçu (pixel art, sans lissage), dimensions, taille du fichier, remplacer l'image, ouvrir l'image |
| Son | fichier, sous-titre, écouter (lecteur du système), commande `/playsound` à copier |
| Graphe | ouvre l'éditeur nodal |

Les textes d'aide sous les champs donnent des repères du jeu (« Pierre : 1,5 ; obsidienne : 1200 »).

**Choisir un item, un bloc ou un son** (sélecteur de référence) : liste éditable qui propose les éléments du projet et accepte un identifiant du jeu ou, là où c'est permis, un tag. Une saisie invalide est expliquée sous le champ.

**Saisie** : chaque modification est appliquée immédiatement et forme une étape d'annulation ; plusieurs frappes rapides dans le même champ (moins de 2 secondes d'écart) n'en forment qu'une. Une valeur hors bornes est ramenée dans les bornes. Un identifiant invalide ou déjà pris est refusé avec un message sous le champ.

## 6. Éditeur nodal

Un onglet par graphe, occupé par le **canevas**. Le panneau **Définition** (nom, variables, paramètres, champs...) est affiché dans l'**Inspecteur**, sous le résumé du graphe, comme « My Blueprint » dans Unreal. Une structure, une interface ou une énumération n'a pas de nœuds : sa définition remplit l'onglet.

### Canevas

- Quadrillage de 32 unités qui suit le déplacement et le zoom ; zoom de 25 % à 200 %, par pas de 10 % à la molette, centré sur la souris.
- **Nœuds** : en-tête coloré par famille, titre, broches d'entrée à gauche, de sortie à droite, valeurs saisissables sur les entrées non reliées (champ texte, case à cocher, sélecteur d'item, liste de constantes). Un nœud en erreur est entouré de rouge.
- **Fils** : courbes de Bézier horizontales ; le fil d'exécution a la couleur du texte du thème, les fils de valeur la couleur de leur type.

| Type | Couleur | Famille de nœud | Couleur d'en-tête |
| --- | --- | --- | --- |
| Exécution | couleur du texte | Événement | `#A23B3B` |
| Booléen | `#D04A3C` | Condition | `#3F7F4A` |
| Nombre | `#3DBA6E` | Action | `#3B6EA5` |
| Texte | `#D980C6` | Valeur | `#A8742A` |
| Joueur | `#4C9BE8` | Fonction | `#6A4AAE` |
| Position | `#E0A53C` | Objet et variable | `#2D7D8C` |
| Item | `#3EC1C9` | | |
| Bloc | `#B07A4F` | | |
| Son | `#A88BE6` | | |
| Objet | `#7FA7FF` | | |
| Énumération | `#C9B458` | | |

### Gestes

| Geste | Effet |
| --- | --- |
| Clic droit (sans glisser) | ouvre le menu d'ajout à cet endroit |
| Clic droit glissé, ou bouton du milieu glissé | déplace la vue |
| Molette | zoom |
| Glisser l'en-tête d'un nœud | déplace le nœud (et les autres nœuds sélectionnés) ; une seule étape d'annulation à la fin du geste |
| Glisser depuis une broche | tire un fil ; lâché sur une broche compatible, il la relie ; lâché dans le vide, il ouvre le menu d'ajout filtré, et le nœud ajouté se branche dessus |
| Alt+clic sur une broche | retire ses fils |
| Clic, Ctrl+clic, rectangle sur le vide | sélection |
| Suppr ou Retour arrière | supprime la sélection (sauf l'entrée d'une fonction, qui serait sans début) |
| Ctrl+D | duplique la sélection (et les fils entre ses nœuds) un peu plus bas à droite |
| Double-clic sur un fil | pose une **redirection** (« reroute » d'Unreal) à cet endroit : le fil passe par ce point, que l'on déplace comme un nœud. Une redirection se dessine comme un point de la couleur du fil (blanc pour l'exécution) ; on tire un fil depuis son côté gauche ou droit. Supprimée, elle laisse le fil en place. Elle ne change rien au programme : la validation et le Java produit sont ceux du graphe sans redirections. |
| Ctrl+A | sélectionne tout |
| F | cadre tout le graphe |
| Échap | annule le geste en cours, ferme le menu |

Un fil refusé est expliqué par un message passager en bas du canevas (« Types incompatibles : Nombre vers Joueur »). Une ligne d'aide rappelle les gestes.

### Menu d'ajout

Recherche en haut (sans tenir compte des accents ni de la casse, sur le titre, la description et la famille), puis les nœuds par famille. Flèches haut et bas pour choisir, Entrée pour ajouter, Échap pour fermer. Filtré par la broche en attente quand un fil a été lâché dans le vide.

### Panneau Définition

Selon la sorte du graphe : nom, accessibilité (avec une explication de chaque niveau), statique, fonction pure, intégration à une classe (lien vers la classe propriétaire) ; listes de paramètres, valeurs renvoyées, variables ou champs (nom, type, valeur par défaut, globale, accessibilité ; ajouter, monter, descendre, supprimer) ; héritage (classe parente, interfaces implémentées) ; méthodes et types intégrés (ajouter une méthode fonction ou procédure, une classe, structure, interface ou énumération intégrée ; ouvrir) ; méthodes à fournir (interface) ; valeurs (énumération).

### Panneau Détails pour un graphe

Nombre d'événements, de nœuds et de fils, état ; liste des problèmes (un clic montre le nœud) ; aide du nœud sélectionné. Les modifications du graphe s'enregistrent avec le projet (Ctrl+S) et s'annulent avec Ctrl+Z.

## 7. Éditeur de code

Un onglet par fichier ouvert depuis l'Explorateur (ou par « Voir le fichier de l'élément », qui montre le JSON de l'élément en lecture seule, mis à jour en direct).

- **Fil d'Ariane** du chemin, **barre d'outils** : Enregistrer (Ctrl+S), Rechercher (Ctrl+F), Formater (Java).
- **Bandeau** selon le statut du fichier : code généré (lecture seule, bouton « Verrouiller pour modifier »), code verrouillé (bouton « Déverrouiller », avec avertissement que les modifications manuelles seront perdues), fichier du projet (« modifiez-le dans Espace de travail > Paramètres du projet »), fichier produit par Gradle ou le jeu, aperçu d'un élément.
- **Texte** : numéros de ligne, coloration (Java, JSON, Markdown, XML, PowerShell, Python), ligne courante surlignée, police à chasse fixe (Cascadia Mono, sinon Consolas).
- **Java** : blocs repliables (accolades), indentation automatique, fermeture des parenthèses, crochets, accolades et guillemets (sauf devant un mot ou après un caractère d'échappement ; taper le caractère fermant juste avant le même le saute), Entrée entre `{` et `}` ouvre un bloc indenté, **complétion** (Ctrl+Espace, ou d'elle-même après 3 lettres s'il y a une correspondance) : mots-clés, types de Minecraft courants avec leur import ajouté automatiquement, extraits (`sout`, `fori`, `subscribe`, `psvm`) et identifiants du fichier ; **formatage** (Ctrl+Maj+F) en une étape d'annulation ; erreurs de la dernière compilation soulignées dans la marge, avec leur message au survol.
- **Rechercher et remplacer** : mot entier, respecter la casse, précédent (Maj+F3), suivant (F3), fermer (Échap).
- Dans un onglet de code, Ctrl+Z annule dans le texte (pas dans le projet).

## 8. Explorateur

- Recherche de fichier, boutons : nouvelle classe Java, nouveau fichier, nouveau dossier, actualiser, tout réduire, ouvrir le dossier dans le système.
- Arborescence du projet, avec une icône par type de fichier ; un point bleu signale un fichier modifié non enregistré. **Elle suit les changements faits sur le disque** par d'autres programmes.
- Double-clic : ouvrir dans un onglet (un fichier binaire ou trop gros s'ouvre avec le programme du système). Clic droit : ouvrir, renommer, mettre à la corbeille, copier le chemin, afficher dans l'explorateur du système.
- **Fichiers protégés** : le fichier projet, `elements/`, `src/`, `src/main/java`... ne se renomment ni ne se suppriment depuis l'Explorateur ; les fichiers des éléments se gèrent depuis le Dashboard.
- **Nouvelle classe Java** : demande le nom (identifiant Java valide), crée le fichier dans le package du dossier choisi avec un modèle (écouteur d'événements du jeu, classe utilitaire, classe vide).
- **Structure** (sous l'arborescence) : les classes, méthodes, champs ou clés du fichier ouvert, avec leur ligne ; un clic y mène.

## 9. Panneau Détails

En haut, la carte **Cible** : légende, badge (ITEM, FICHIER, LECTURE SEULE...), titre, sous-titre (identifiant complet ou chemin). Puis selon la cible :

- **élément sélectionné dans le Dashboard** : son éditeur complet (les mêmes onglets que l'onglet d'édition) ;
- **onglet d'un élément actif** : rien de plus (tout est dans l'onglet) ;
- **fichier de code** : propriétés (type, encodage, indentation, lignes, curseur, état), structure, boutons Enregistrer, Recharger, Afficher dans le système ;
- **graphe** : voir la section 6.

Sans cible : « Sélectionnez un élément dans le Dashboard, ou ouvrez un fichier depuis l'Explorateur, pour voir ses détails. »

## 10. Sortie et Diagnostics

- **Sortie** : le journal de l'éditeur, de la génération et de Gradle, horodaté, coloré par niveau, défilement automatique, bouton Effacer. Rappelle que le journal est aussi enregistré sur le disque.
- **Diagnostics** : résumé (« 0 erreur(s), 21 avertissement(s) »), bouton Valider le projet, tableau Gravité / Message / Source / Origine (Validation ou Build). Double-clic : ouvre l'élément concerné, ou le fichier Java à la bonne ligne. La validation remplace les résultats de validation ; un build remplace les résultats du build précédent ; les deux coexistent.

## 11. Fenêtres secondaires

- **Nouveau projet / Paramètres du projet** : nom du mod, identifiant (« Définitif après la création »), version, auteurs, site web, licence, description, MDK (liste des MDK utilisables, avec rappel de ce qu'ils fixent et lien « Gérer les MDK... »), emplacement (dossier parent, « Le projet sera créé dans : ... »). Chaque champ invalide est expliqué. Les paramètres d'un projet existant se modifient en une seule étape d'annulation.
- **Paramètres de l'éditeur**, quatre onglets :
  - **Général** : rouvrir le dernier projet au démarrage, intervalle de sauvegarde automatique, nombre de sauvegardes de secours ; bouton « Paramètres par défaut » en bas de la fenêtre (les thèmes et langues importés sont conservés).
  - **Apparence** : liste des thèmes avec un aperçu (fond, carte, texte, accent) et leur origine ; boutons Importer, Exporter, Supprimer (thèmes importés seulement). Couleur d'accent : celle du thème ou une des sept proposées (bleu, violet, vert, orange, rouge, rose, cyan). Taille de l'interface : 80 %, 90 %, 100 %, 110 %, 125 %, 150 %. Thème et accent sont appliqués immédiatement en aperçu et rétablis si l'on annule ; la taille est appliquée à l'enregistrement.
  - **Langue** : liste des langues avec leur origine et, pour une langue importée, la part des textes traduits ; boutons Importer, Exporter un modèle, Supprimer. Après l'enregistrement d'une autre langue, Mine Engine propose de redémarrer ; il ferme alors la fenêtre (le projet modifié peut être enregistré) et se relance sur le même projet.
  - **Éditeurs** : taille du texte et retour à la ligne de l'éditeur de code ; affichage de la grille et calage des nœuds sur la grille (pas de 32) dans l'éditeur de graphes.
- **État retenu entre deux sessions** : barre latérale de l'accueil réduite ou non.
- **Gestionnaire de MDK** : explication de ce qu'est un MDK, versions prises en charge, dossier de la bibliothèque ; tableau des MDK installés (MDK, Minecraft, loader, version du loader, Java, Gradle, état, origine) avec Supprimer et Ouvrir le dossier ; MDK officiels à télécharger ; importer une archive ou un dossier ; actualiser.
- **Saisie d'un texte** (nouveau dossier, renommer...) : titre, question, champ, OK et Annuler.
- **Confirmation à la fermeture** d'un projet ou d'un onglet modifié : Enregistrer, Ne pas enregistrer, Annuler. Pendant un build, la fermeture propose d'arrêter le build.

## 12. Apparence

### Couleurs

Toutes les couleurs viennent d'une palette par thème ; aucune couleur n'est écrite en dur dans les vues (sauf celles de l'éditeur nodal, les mêmes dans les deux thèmes, comme dans Unreal).

| Rôle | Sombre | Clair |
| --- | --- | --- |
| Barres latérales, panneaux ancrés | `#1E1F22` | `#E6E7EA` |
| Zone centrale | `#2B2C30` | `#F3F4F6` |
| Carte | `#34363B` | `#FFFFFF` |
| Carte survolée | `#3B3D43` | `#F0F1F4` |
| Bordure | `#3E4046` | `#D9DBE0` |
| Carte sélectionnée / bordure | `#22344C` / `#3B8EEA` | `#E8F1FC` / `#2F7FD8` |
| Navigation sélectionnée | `#233C5C` | `#DCE9FA` |
| Accent (bouton principal, onglet actif) | `#2F7FD8` (survol `#3B8EEA`, pressé `#2668B3`) | idem |
| Trait d'accent, logo | `#3B8EEA` | `#2F7FD8` |
| Badge (fond / texte) | `#3A3C42` / `#C3C6CD` | `#E6E7EA` / `#4A4D55` |
| Lien | `#6CB6FF` | `#1F66C1` |
| Texte fort | `#F3F4F6` | `#1B1C1F` |
| Texte secondaire | `#9C9FA6` | `#5F626A` |
| Journal : debug, succès, avertissement, erreur | `#8A8A94`, `#4CC37A`, `#E0A93A`, `#FF6B6B` | `#7A7A86`, `#1E8449`, `#9A6200`, `#C0392B` |
| Code : ligne courante, sélection, ligne en erreur | `#303237`, `#264F78`, `#3A1F24` | `#F0F1F4`, `#ADD6FF`, `#FBE3E4` |

Les contrôles standard (champs, boutons, listes déroulantes, menus, info-bulles) prennent ces gris, pas ceux du système. Le thème « identique au système » suit le réglage clair ou sombre du système et change avec lui.

#### Thèmes livrés

Les couleurs des thèmes sont des données (un dictionnaire nom de couleur vers `#RRGGBB`), pas des fichiers d'interface : le même format sert aux thèmes livrés, importés et exportés. Sombre et Clair définissent toutes les couleurs ci-dessus. Les cinq thèmes de couleur sont construits à partir de cinq teintes (fond, barre latérale, cartes, texte, accent) ; survols, bordures, champs, texte secondaire et sélections en sont dérivés par mélange :

| Thème | Fond | Barre latérale | Cartes | Texte | Accent |
| --- | --- | --- | --- | --- | --- |
| Bleu nuit | `#0D1433` | `#0A0F28` | `#171F55` | `#E8EDF8` | `#49699A` |
| Rouge carmin | `#19171B` | `#121014` | `#271318` | `#F2E8EA` | `#75020F` |
| Vert forêt | `#09231A` | `#101C13` | `#0B372B` | `#E6F2EC` | `#3E795F` |
| Or | `#19171B` | `#121014` | `#2F2921` | `#F2EEE4` | `#9E8123` |
| Violet | `#1E202C` | `#17181F` | `#292A37` | `#ECECF4` | `#60519B` |

Contraste élevé : fonds noirs, textes blancs, bordures grises marquées.

#### Couleur d'accent

Choisir un accent recalcule toutes les couleurs qui en dépendent : bouton principal (accent), survol (accent éclairci de 12 %), clic (assombri de 15 %), trait d'accent et bordure de carte sélectionnée, puis les fonds de sélection, mélanges de l'accent et du fond (barre latérale en sombre, carte en clair) à 22, 30, 40 et 50 % en sombre, 10, 17, 23 et 31 % en clair.

### Police

**Roboto** (Apache 2.0), intégrée à l'application, pour toute l'interface ; code et chemins en Cascadia Mono, sinon Consolas.

### Icônes

Règles dans [docs/ICONS.md](../docs/ICONS.md), liste dans `Themes/Icons.xaml`. En bref : icônes au trait, monochromes, de la couleur du texte qui les entoure, 12 px dans les boutons d'outils, 13 px à côté d'un texte, 16 px pour les sortes d'éléments. Les glyphes viennent de la police d'icônes du système (Segoe Fluent Icons, sinon Segoe MDL2 Assets) ; les icônes absentes de la police sont dessinées en vectoriel sur un repère de 16 × 16 : cube (logo), bloc, item (gemme), recette (grille), entité, mob (patte), tag, armure, commande, fluide, potion, graphe nodal, fonction, procédure, classe, énumération, structure, interface, table de butin, outil, Forge, NeoForge, Fabric, Quilt, datapack. **Le code désigne une icône par son nom, jamais par un code de caractère.** Les textures du mod sont affichées en pixel art (sans lissage) et jamais recolorées.

### Langue

Interface en anglais par défaut, français au choix (voir [10](10-comportements-transverses.md)). Les dates et les nombres suivent la langue choisie.

## 13. Raccourcis

| Raccourci | Action |
| --- | --- |
| Ctrl+N / Ctrl+O | nouveau projet / ouvrir un projet |
| Ctrl+S | enregistrer tout (le projet et les onglets modifiés) |
| Ctrl+W | fermer l'onglet |
| Ctrl+Z / Ctrl+Y | annuler / rétablir (dans le texte quand un onglet de code a le focus) |
| Ctrl+B, Ctrl+J, Ctrl+Alt+B | Explorateur, panneau du bas, Détails |
| F7 | valider le projet |
| Ctrl+Maj+B | générer le mod |
| F5 | lancer Minecraft |
| Ctrl+, | paramètres de l'éditeur |
| Retour arrière (Dashboard) | dossier parent |
| Ctrl+F, F3, Maj+F3, Échap (code) | rechercher, suivant, précédent, fermer |
| Ctrl+Espace, Ctrl+Maj+F (Java) | complétion, formatage |
| Suppr, Ctrl+D, Ctrl+A, F, Échap (graphe) | supprimer, dupliquer, tout sélectionner, cadrer, annuler le geste |
