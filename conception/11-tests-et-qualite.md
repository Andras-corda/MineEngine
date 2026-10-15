# 11. Tests et qualité

## Philosophie

Mine Engine produit du code qu'un autre outil (Gradle, le compilateur Java, Minecraft) doit accepter. Un défaut du générateur ne se voit souvent qu'au moment de compiler, ou en jeu. Les tests doivent donc couvrir trois niveaux :

1. **Le modèle** : règles de validation, identifiants, références, annulation. Rapides, nombreux, sans disque si possible.
2. **Les fichiers** : enregistrer puis relire un projet redonne le même projet ; enregistrer deux fois produit des fichiers identiques à l'octet ; la génération écrit les bons fichiers aux bons endroits, avec le bon contenu pour chaque version.
3. **La chaîne réelle** : un mod d'exemple complet est généré et compilé par Gradle avec chaque MDK pris en charge. Lent (plusieurs minutes, plusieurs centaines de Mo téléchargés la première fois), donc lancé à la demande.

La version 0.4.5 compte 121 tests, dont 3 de bout en bout.

## Ce qui doit être testé

Cette liste reprend les tests existants. Une réécriture doit les reproduire avant d'ajouter des fonctionnalités : ils forment le filet de sécurité du portage.

### Identifiants et références

- Validation des identifiants (minuscules, chiffres, `_`, longueur, premier caractère) ; identifiant du mod d'au moins 2 caractères.
- Construction d'un identifiant à partir d'un texte libre (« Épée magique », « 42 blocs », « ??? »).
- Suffixe numérique pour un identifiant déjà pris ; refus d'un doublon dans le même espace ; une texture peut porter le nom d'un item, deux items non.
- Aller-retour d'une référence de contenu par son texte enregistré (élément, identifiant, tag).

### Éléments

- Bornes refusées ou ramenées (taille de pile, dureté...).
- Toutes les options d'un item et d'un bloc survivent à un enregistrement ; un ancien fichier sans les nouvelles options reste lisible.
- Renommer une texture utilisée par trois items laisse les trois items fonctionnels.
- Supprimer un élément utilisé est signalé comme lien cassé ; un bloc sans forme item ne peut pas servir d'item.
- Validation des mobs (comportements impossibles) ; mobs et recettes survivent à un rechargement.
- Ancien format d'éléments converti (textures devenues éléments, item laissé devenu référence).

### Dossiers

- Les dossiers existants comprennent les dossiers déclarés et ceux qui contiennent des éléments.
- Renommer un dossier déplace ses éléments, et s'annule.
- Supprimer un dossier fait remonter son contenu d'un niveau.
- Normalisation des chemins.

### Annulation

- Une modification de propriété s'annule et se rétablit.
- Des frappes successives fusionnent en une étape ; une pause ou un scellement en fait deux.
- Une nouvelle modification vide « rétablir ».
- Supprimer un élément s'annule à la même position.
- Les paramètres du projet s'annulent en une étape.

### Projets

- Un nouveau projet porte le nom de son dossier et se rouvre depuis son fichier projet.
- Un projet enregistré se rouvre à l'identique ; enregistrer deux fois produit des fichiers identiques ; un élément renommé ne laisse pas son ancien fichier.
- Créer un projet dans un dossier non vide échoue.
- Un ancien projet est converti (scripts rangés dans leur package, mondes de test conservés).
- Sauvegardes de secours : l'intervalle est respecté, seules les plus récentes sont gardées.
- Projets connus : un seul par dossier, avec l'ouverture la plus récente ; une liste illisible donne une liste vide ; l'aperçu compte les éléments sans ouvrir le projet.
- Le journal fichier et le journal composite écrivent chaque message.

### Graphes et programmation

- Règles de branchement (Blueprints) ; boucle d'exécution en erreur ; boucle de valeurs refusée.
- La validation signale les problèmes sur les bons nœuds.
- Les graphes survivent à un rechargement et suivent un item renommé.
- Un graphe sans événement ne produit pas de comportement.
- Un exemple de programme complet (énumération, structure, interface, classe avec héritage, méthodes, fonctions) est valide et se traduit en définitions.
- Une sous-classe alimente les entrées de sa parente et de ses interfaces.
- Une définition privée n'est visible que là où elle est intégrée.
- Héritage et interfaces vérifiés (méthodes manquantes, signatures).
- Les définitions survivent à un rechargement et deviennent des classes Java du package `code`.
- Un nouveau graphe de chaque sorte commence sans erreur.
- Le menu d'ajout ne propose que ce que le graphe peut utiliser.
- Un événement d'item doit viser un item du mod, et son code est appelé depuis la classe de l'item.

### Génération

- Le projet est traduit en une IR triée, avec des textures de remplacement.
- NeoForge : ressources et sources Java écrites ; Forge 1.20.1 : registres Forge et dossiers de données au pluriel.
- Recettes au format de chaque version ; 1.20.1 utilise l'ancienne API.
- Outils : dégâts passés au constructeur en 1.20.1, attributs dans les propriétés en 1.21.1.
- Mobs : classes d'entité, enregistrement de l'IA et ressources ; API d'entité ancienne en 1.20.
- La carte des sources pointe sur les lignes Java de chaque élément.
- Un MDK qui ne correspond pas au projet est une erreur.
- `gradle.properties` mis à jour et échappé ; l'argument JVM ajouté une seule fois ; valeurs de `mods.toml` protégées de l'expansion Gradle.
- Noms Java sans collision avec les champs générés.
- Un graphe « interaction, possède l'item, faire apparaître » se compile en une apparition conditionnelle ; les graphes produisent une classe de comportement inscrite par la classe principale.
- La génération garde le code de l'utilisateur et les fichiers verrouillés, et supprime les fichiers générés devenus inutiles.
- Les modèles de classes Java suivent le loader et le package du dossier.
- Le formateur Java réindente selon les accolades et les `case`, respecte chaînes, commentaires et parenthèses ouvertes, et ne change pas les modèles de classes.

### MDK et compilation

- Les versions de Minecraft se comparent numériquement.
- L'inspection détecte le loader et les versions ; un dossier sans Gradle wrapper est refusé.
- La bibliothèque importe les archives et détecte les fichiers déposés.
- Le registre des backends n'accepte que les versions prises en charge.
- La version de Gradle limite le Java utilisé pour le lancer.
- Les erreurs du compilateur Java sont rattachées à l'élément qui a produit la ligne.

### Langues

- L'anglais est la langue par défaut ; le français n'a pas besoin de traduction ; l'anglais traduit.
- Tout texte de l'interface a sa traduction anglaise, avec les mêmes `{n}`.

### Bout en bout

Pour chaque MDK pris en charge (`forge-1.20.1-47.4.10`, `forge-1.21.1-52.1.0`, `neoforge-1.21.1-21.1.252`) : téléchargement du MDK si nécessaire, création d'un projet d'exemple riche (items de toutes sortes, blocs, mobs, recettes, sons, graphes d'événements et de programmation), génération, compilation Gradle, présence du .jar. Les projets d'exemple restent dans un dossier temporaire (`mineengine-e2e`), ce qui permet aussi de les ouvrir dans l'éditeur pour des essais manuels. Ces tests ne tournent que si la variable d'environnement `MINEENGINE_E2E` vaut `1`.

## Outils de test

- **Horloge manuelle** : tout ce qui dépend du temps (fusion des frappes, sauvegardes de secours, journal) reçoit une horloge injectable, avancée à la main dans les tests.
- **Dossier temporaire** créé et supprimé par test.
- **Contenu d'exemple** et **programme d'exemple** réutilisables, qui exercent toutes les options.
- **Constructeur de graphes** pour écrire un graphe de test en quelques lignes.

## Essais de l'interface

L'interface n'a pas de tests automatisés dans la version 0.4.5. Elle a été vérifiée par des scripts qui lancent l'application sur une copie d'un projet d'exemple, simulent des clics et des touches, et prennent des captures d'écran. Ces scripts sauvegardent puis restaurent la liste des projets connus de l'utilisateur, pour ne jamais toucher à ses vrais projets.

Pour la réécriture, il est conseillé de séparer strictement l'état de l'interface (view models) de son rendu, et de tester les view models comme du code ordinaire : création d'éléments, navigation dans les dossiers, gestes de l'éditeur nodal traduits en commandes.

## Règles de qualité du code

Elles ont guidé la version C# et restent valables :

- **Programmation orientée objet stricte** : responsabilités uniques, dépendances par le constructeur, pas d'état global (hors traductions), objets valeur immuables pour les identifiants et les références.
- **Aucun emoji** dans le code, les messages ou les documents.
- **Commentaires et documentation en français**, noms de code en anglais.
- **Messages à l'utilisateur** complets et utiles (voir [10](10-comportements-transverses.md)).
- **Déterminisme** : mêmes entrées, mêmes fichiers (ordre des clés, tris, fins de ligne), pour des diffs Git propres et des tests fiables.
