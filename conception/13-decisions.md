# 13. Décisions

Les choix structurants du projet, avec leur raison. Avant de revenir sur l'un d'eux, il faut savoir pourquoi il a été pris. Chaque décision indique la version où elle est apparue.

---

### D1. Le projet est le projet Gradle du mod (V0.4.2)

**Contexte.** Jusqu'à la V0.4.1, le mod était généré dans un sous-dossier `Generated/` réécrit à chaque build, séparé des fichiers de Mine Engine. L'utilisateur ne pouvait pas ajouter de code Java durable.

**Décision.** Comme un workspace MCreator, le dossier du projet est lui-même le projet Gradle. Le code de l'utilisateur vit dans `src/main/java` à côté du code généré.

**Raison.** Un projet doit pouvoir s'ouvrir dans IntelliJ IDEA et se compiler en ligne de commande ; le passage au code doit être progressif.

**Conséquences.** Il faut distinguer ce que Mine Engine a généré de ce que l'utilisateur a écrit (D6), et ne jamais supprimer un fichier qu'on n'a pas généré.

---

### D2. Un fichier JSON lisible par élément (V0.1)

**Décision.** Chaque élément est un fichier `.asset.json` indenté, rangé par sorte dans `elements/`. Le format est déterministe (ordre des clés, tris, LF).

**Raison.** Les projets doivent se versionner dans Git avec des diffs lisibles, et se réparer à la main si besoin. Un fichier par élément limite les conflits de fusion.

**Conséquences.** Enregistrer deux fois doit produire les mêmes octets ; c'est testé. Un format binaire ou une base de données sont exclus.

---

### D3. Identifiant interne stable, distinct de l'identifiant en jeu (V0.3)

**Décision.** Chaque élément a un UUID immuable ; les liens entre éléments passent par lui. L'identifiant en jeu (`magic_sword`) est renommable.

**Raison.** Renommer un élément ne doit rien casser. Dans les premières versions, un renommage cassait les recettes et les drops.

**Conséquences.** Les références de contenu ont trois formes (élément, identifiant du jeu, tag) ; la résolution en identifiant du jeu se fait seulement à la génération.

---

### D4. Une représentation intermédiaire entre le projet et le générateur (V0.1)

**Décision.** Le projet est validé puis traduit en Mod IR ; le générateur ne lit que l'IR.

**Raison.** Le générateur ne doit pas dépendre du modèle d'édition ; ajouter un loader ou une version de Minecraft ne doit toucher qu'au générateur. Toutes les références sont résolues, tous les cas particuliers ramenés à une forme simple (quantités dans l'ordre, outils sans durabilité...), avant la génération.

---

### D5. S'appuyer sur les MDK officiels (V0.1)

**Décision.** Mine Engine ne contient pas de modèle de projet Gradle : il recopie le MDK officiel du loader, installé dans une bibliothèque, puis l'adapte (`gradle.properties`, `mods.toml`).

**Raison.** Les MDK évoluent ; les reproduire serait une maintenance sans fin. L'utilisateur peut importer un MDK plus récent sans attendre une nouvelle version de Mine Engine (même si le générateur doit encore le prendre en charge).

**Conséquences.** Un backend par loader, qui dit quels MDK il sait utiliser. Changer de MDK vide les dossiers de travail de Gradle.

---

### D6. Liste des fichiers générés et fichiers verrouillés (V0.4.2)

**Décision.** Chaque génération enregistre la liste de ce qu'elle a écrit. Seuls ces fichiers sont réécrits ou supprimés. L'utilisateur peut verrouiller un fichier généré pour le modifier à la main ; il n'est alors plus réécrit.

**Raison.** Reprise du « code verrouillé » de MCreator : un programmeur doit pouvoir sortir du cadre sans tout perdre au build suivant.

---

### D7. Tout passe par l'historique d'annulation (V0.2)

**Décision.** Aucune vue ne modifie le modèle directement ; tout passe par une commande annulable. Les sous-parties complexes (graphes, listes) sont immuables et se remplacent entières.

**Raison.** Un débutant doit pouvoir tout essayer sans crainte. Rendre les sous-parties immuables rend l'annulation triviale et sûre.

---

### D8. La programmation nodale suit les Blueprints d'Unreal (V0.4, V0.4.3)

**Décision.** Fils d'exécution et fils de valeur séparés ; nœuds purs recalculés à chaque utilisation ; règles de branchement d'Unreal ; fonctions, procédures, classes, structures, interfaces, énumérations comme dans les Blueprints.

**Raison.** Les blocs empilés de MCreator deviennent illisibles. Le modèle d'Unreal a fait ses preuves auprès de non-programmeurs et passe à l'échelle.

**Conséquences.** La sorte d'un graphe est choisie à sa création et ne change plus. Un nœud ne stocke pas ses broches : elles se déduisent de sa sorte et des définitions, pour suivre les changements de signature.

---

### D9. Le monde en premier paramètre de toute méthode générée (V0.4.3)

**Décision.** Toute fonction, procédure, méthode et tout constructeur Java généré reçoit `Level level`.

**Raison.** Comme le « World Context » d'Unreal : les nœuds qui agissent sur le jeu (faire apparaître un item, jouer un son) doivent fonctionner partout, pas seulement dans un graphe d'événements.

---

### D10. Les dossiers sont logiques, et servent de modules (V0.4.1, V0.4.3)

**Décision.** Les dossiers du Dashboard ne déplacent aucun fichier ; une définition privée hors de toute classe n'est visible que depuis son dossier et ses sous-dossiers.

**Raison.** Ranger un projet ne doit pas risquer de casser des chemins ; et un projet de taille moyenne a besoin d'une notion de module, que les dossiers fournissent sans concept supplémentaire.

---

### D11. Validation continue, seules les erreurs bloquent (V0.2)

**Décision.** Le projet est revalidé 400 ms après chaque modification. Trois gravités ; seule l'erreur empêche de générer.

**Raison.** Les problèmes doivent apparaître pendant l'édition, pas au moment du build. Un avertissement ne doit pas empêcher d'essayer.

---

### D12. Les erreurs Java sont ramenées aux éléments (V0.2)

**Décision.** La génération produit une carte des sources ; les erreurs du compilateur sont rattachées à l'élément (et au nœud) qui a produit la ligne.

**Raison.** Un utilisateur qui n'a pas écrit de Java ne doit pas avoir à lire du Java pour comprendre une erreur.

---

### D13. Une texture manquante n'empêche pas de compiler (V0.1)

**Décision.** Sans texture, ou avec un fichier disparu, le damier magenta et noir de Minecraft est utilisé, avec un avertissement.

**Raison.** On doit pouvoir tester un comportement avant d'avoir dessiné ses textures.

---

### D14. Versions prises en charge : Forge 1.20.1 et 1.21.1, NeoForge 1.21.1 (V0.1 à V0.4)

**Décision.** Le générateur ne vise que ces trois cibles, testées de bout en bout.

**Raison.** Chaque version change des API ; mieux vaut trois cibles fiables que dix approximatives. Fabric est repoussé à la V1.6.

---

### D15. Interface en anglais par défaut, textes français dans le code (V0.4.5)

**Décision.** Les textes restent écrits en français à l'endroit où ils servent ; un catalogue les traduit. L'anglais est la langue par défaut, le français au choix ; changer de langue demande un redémarrage.

**Raison.** Le projet est développé en français mais vise un public international. Garder le texte source dans le code évite d'inventer 1 500 clés et rend le code lisible ; le redémarrage évite de rendre chaque texte dynamique.

**Conséquences.** Un test garantit qu'aucun texte n'est oublié. Un mot à deux sens a deux textes. Voir [12](12-portage-rust.md) pour une alternative à clés neutres.

---

### D16. Une interface calme : gris neutres, un accent bleu, Roboto, icônes au trait (V0.4.2 à V0.4.5)

**Décision.** Palette de gris neutres contrastés (barres latérales les plus sombres, zone centrale plus claire, cartes plus claires encore), un seul accent bleu, police Roboto intégrée, icônes monochromes au trait, de la couleur du texte qui les entoure.

**Raison.** L'outil doit ressembler à un environnement de création (VS Code, Unreal), pas à un utilitaire Windows. L'utilisateur passe des heures dedans.

---

### D17. Accueil épuré (V0.4.5)

**Décision.** Les actions de projet (nouveau, ouvrir, ajouter) sont dans la barre latérale de l'accueil, réductible à ses icônes. La page Accueil est vide pour l'instant ; la liste des projets est dans « Mes projets ». Pas de panneau « Projet sélectionné ».

**Raison.** Les pages Accueil et Mes projets faisaient doublon ; l'aperçu détaillé n'apportait pas assez pour la place qu'il prenait.

---

### D18. Aucune dépendance superflue dans le cœur

**Décision.** Le cœur et le générateur n'utilisent que la bibliothèque standard (JSON, archives, HTTP) ; l'encodeur PNG de la texture de remplacement est écrit à la main.

**Raison.** Moins de dépendances, moins de surprises de mise à jour, et un portage plus simple.

---

### D19. Programmation orientée objet stricte, pas d'emoji

**Décision.** Responsabilités uniques, dépendances par le constructeur, une seule racine de composition ; aucun emoji dans le code, les messages ou les documents ; commentaires en français.

**Raison.** Choix de l'auteur du projet, pour un code durable et lisible.
