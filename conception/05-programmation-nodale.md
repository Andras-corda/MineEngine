# 05. Programmation nodale

La programmation nodale est ce qui distingue Mine Engine de MCreator. Elle reprend le modèle des Blueprints d'Unreal Engine : des nœuds posés sur un canevas, reliés par des fils d'exécution (l'ordre des instructions) et des fils de valeur (les données). Ce document décrit le modèle complet, indépendamment de l'éditeur visuel (voir [09](09-interface-utilisateur.md)) et de la traduction en Java (voir [07](07-generation-du-mod.md)).

## 1. Ce qu'un graphe peut définir

Un graphe est un élément du mod. À sa création, l'utilisateur choisit ce qu'il définit ; ce choix ne change plus.

| Sorte | A un corps en nœuds | S'appelle | Est un type | Description |
| --- | --- | --- | --- | --- |
| `Events` | oui | non | non | Réactions aux événements du jeu. C'est le graphe des premières versions. |
| `Function` | oui | oui | non | Suite d'instructions qui renvoie une ou plusieurs valeurs. |
| `Procedure` | oui | oui | non | Suite d'instructions qui ne renvoie rien. |
| `Class` | oui (le constructeur) | non | oui | Champs, constructeur, méthodes ; peut hériter d'une classe et implémenter des interfaces. |
| `Struct` | non | non | oui | Des champs, créée avec la valeur de chacun. |
| `Interface` | non | non | oui | Des méthodes que les classes qui l'implémentent doivent fournir. |
| `Enum` | non | non | oui | Une liste de valeurs nommées. |

Un graphe qui n'est pas d'événements est appelé une **définition**.

## 2. Ce que porte un graphe

| Propriété | Sortes concernées | Description |
| --- | --- | --- |
| Accessibilité | toutes sauf `Events` | `Public`, `Protected`, `Private` (voir la visibilité) |
| Propriétaire | fonctions, procédures, types | la classe dans laquelle la définition est intégrée (méthode, type imbriqué), ou aucune |
| Statique | méthodes | méthode d'une classe qui ne s'exécute pas sur un objet (pas de « Moi ») |
| Pure | fonctions | sans effet sur le jeu : son nœud d'appel n'a pas de fil d'exécution |
| Paramètres | fonctions, procédures, classes (paramètres du constructeur) | liste de variables |
| Valeurs renvoyées | fonctions | liste de variables (au moins une, sinon avertissement) |
| Variables | événements (variables du graphe, conservées tant que le jeu tourne), fonctions et procédures (locales, ou globales si « conservées d'un appel à l'autre »), classes et structures (champs) | liste de variables |
| Classe parente | classes | une classe, ou aucune |
| Interfaces implémentées | classes | liste d'interfaces |
| Valeurs | énumérations | liste de constantes (identifiant, nom) |
| Méthodes | interfaces | liste de signatures (identifiant, nom, paramètres, valeurs renvoyées ; au plus une valeur renvoyée) |
| Corps | sortes qui ont un corps | les nœuds et les fils |

**Une variable** (paramètre, valeur renvoyée, variable, champ) a un identifiant stable (les nœuds qui l'utilisent suivent un renommage), un nom, un type, une valeur par défaut (texte au format des valeurs saisies sur les nœuds), une accessibilité (pour les champs) et un drapeau « globale » (pour les variables de fonction).

Toutes ces propriétés sont immuables au sens de l'historique : en changer une remplace la liste entière, ce qui fait une seule étape d'annulation.

## 3. Types de données

| Type | Valeur saisissable sur le nœud | Texte enregistré | Java |
| --- | --- | --- | --- |
| Exécution | non | (pas un type de variable) | |
| Booléen | oui (`true`/`false`) | `boolean` | `boolean` |
| Nombre (entier) | oui, de -1 000 000 à 1 000 000 | `number` | `int` |
| Texte | oui | `text` | `String` |
| Joueur | non | `player` | `Player` |
| Position | non | `position` | `BlockPos` |
| Item | oui (référence) | `item` | `Item` |
| Bloc | oui (référence) | `block` | `Block` |
| Son | oui (référence) | `sound` | `SoundEvent` |
| Objet (classe, structure, interface du projet) | non | `object:<uuid>` | le nom de la classe |
| Énumération du projet | oui (une constante) | `enum:<uuid>` | le nom de l'énumération |

**Compatibilité** d'une sortie vers une entrée : même type ; ou nombre vers texte (converti automatiquement) ; ou objet d'une classe vers une entrée de sa classe parente (à tout niveau) ou d'une interface qu'elle (ou une parente) implémente.

**Valeur par défaut** d'une entrée de type défini par une variable : celle de la variable si elle est renseignée, sinon 0, faux, vide, ou la première constante d'une énumération.

## 4. Nœuds, broches et fils

### Le nœud

Un nœud a un identifiant, une sorte, une position sur le canevas, et les valeurs saisies sur ses entrées non reliées (texte brut : `3`, `true`, `minecraft:diamond`, `asset:<uuid>`). Les nœuds de programmation désignent en plus une définition (`target` : la fonction appelée, la classe créée...) et un membre (`member` : un champ, une variable, une méthode d'interface).

Les broches d'un nœud ne sont pas enregistrées : elles se déduisent de sa sorte (catalogue fixe) ou de la définition qu'il vise (modèle de programme). Ainsi, ajouter un paramètre à une fonction ajoute l'entrée correspondante à tous ses nœuds d'appel.

### Le flux d'exécution

| Flux | Entrée d'exécution | Sorties d'exécution | Exemples |
| --- | --- | --- | --- |
| `Start` | non | `then` | événements, entrée d'une fonction |
| `Step` | `exec` | `then` | actions, appels, créations d'objet, affectations |
| `Branch` | `exec` | `true` et `false` | conditions |
| `End` | `exec` | aucune | retour |
| `Pure` | non | aucune | valeurs, calculs, lecture d'un champ, fonction pure |

### Une broche

Une broche a un nom technique (stable, enregistré dans les fils et les valeurs), un libellé, un type, une valeur par défaut éventuelle, et deux drapeaux :

- **reliable** : faux pour une entrée qui n'est qu'un champ du nœud (la valeur d'un nœud « Nombre ») ;
- **facultative** : vraie pour l'entrée « Objet » d'une méthode appelée depuis sa propre classe ; vide, elle désigne « Moi ».

Une entrée d'énumération porte la liste de ses valeurs possibles.

### Règles de branchement

Ce sont celles d'Unreal :

- une entrée de valeur ne reçoit qu'un fil ; une sortie de valeur peut en envoyer plusieurs ;
- une sortie d'exécution n'envoie qu'un fil ; une entrée d'exécution peut en recevoir plusieurs ;
- un nouveau fil remplace l'ancien là où un seul est permis.

Un fil est refusé, avec une explication, si :

- un des nœuds n'existe pas, ou c'est le même nœud ;
- un fil d'exécution n'arrive pas sur l'entrée `exec` d'un nœud qui en a une ;
- une valeur arrive sur une entrée d'exécution, ou une broche n'existe pas ;
- l'entrée n'est pas reliable (« L'entrée X se règle directement sur le nœud ») ;
- les types sont incompatibles (« Types incompatibles : Nombre vers Joueur ») ;
- le fil fermerait une boucle de valeurs (la source dépend déjà, par des fils de valeur, du nœud visé).

## 5. Catalogue des nœuds

Les noms techniques des broches sont entre crochets. « Joueur » et « Position » en entrée n'ont pas de valeur saisissable : ils doivent être reliés.

### Événements (flux `Start`, graphe d'événements seulement)

| Nœud | Sorties | Annulable |
| --- | --- | --- |
| `OnRightClickBlock` : Clic droit sur un bloc (main principale) | Joueur [player], Position du bloc [position] | oui |
| `OnUseItem` : Utilisation d'un item (clic droit dans le vide) | Joueur, Position du joueur | oui |
| `OnPlayerJoin` : Connexion d'un joueur | Joueur, Position du joueur | non |
| `OnBlockBroken` : Bloc cassé par un joueur | Joueur, Position du bloc | oui |

### Événements d'un item du mod (flux `Start`)

Chacun a une entrée non reliable « Item du mod » [item] qui doit désigner un **item du projet** (pas un item de Minecraft, pas un bloc), et deux sorties Joueur et Position.

| Nœud | Déclenchement | Position |
| --- | --- | --- |
| `OnItemRightClick` | clic droit dans le vide avec l'item | du joueur |
| `OnItemUseOnBlock` | clic droit sur un bloc avec l'item | du bloc |
| `OnItemHitEntity` | l'item frappe une créature | de la créature |
| `OnItemBlockMined` | un bloc est cassé avec l'item | du bloc |
| `OnItemConsumed` | l'item est mangé ou bu (item comestible) | du joueur |
| `OnItemCrafted` | l'item est fabriqué | du joueur |
| `OnItemHeldTick` | à chaque tick (20 par seconde) tant que l'item est tenu | du joueur |

### Conditions (flux `Branch`)

| Nœud | Entrées (défaut) |
| --- | --- |
| `Branch` : Si | Condition [condition] booléen (vrai) |
| `HasItem` : Possède l'item (au moins la quantité) | Joueur, Item [item] (`minecraft:diamond`), Quantité [count] (1) |
| `HoldsItem` : Tient l'item en main principale | Joueur, Item (`minecraft:stick`) |
| `IsSneaking` : Est accroupi | Joueur |
| `Chance` : vrai au hasard | Pourcentage [percent] (50) |

### Actions (flux `Step`)

| Nœud | Entrées (défaut) |
| --- | --- |
| `SpawnItem` : Faire apparaître un item au sol | Position, Item (`minecraft:diamond`), Quantité (1) |
| `GiveItem` : Donner un item (au sol si l'inventaire est plein) | Joueur, Item (`minecraft:diamond`), Quantité (1) |
| `RemoveItem` : Retirer au plus la quantité | Joueur, Item (`minecraft:diamond`), Quantité (1) |
| `SendMessage` : Envoyer un message dans le chat du joueur | Joueur, Message [message] texte (« Bonjour ! ») |
| `PlaySound` : Jouer un son à la position | Position, Son [sound] (`minecraft:entity.experience_orb.pickup`) |
| `SetBlock` : Placer un bloc | Position, Bloc [block] (`minecraft:stone`) |
| `Explosion` | Position, Puissance [power] (2 ; TNT : 4) |
| `CancelEvent` : Annuler l'événement | aucune ; seulement dans un graphe d'événements, après un événement annulable |

### Valeurs et calculs (flux `Pure`)

| Nœud | Entrées (défaut) | Sortie |
| --- | --- | --- |
| `NumberValue` | Valeur [value] non reliable (0) | Nombre [value] |
| `TextValue` | Valeur non reliable (vide) | Texte [value] |
| `BooleanValue` | Valeur non reliable (vrai) | Booléen [value] |
| `PlayerPosition` : bloc des pieds du joueur | Joueur | Position [position] |
| `PlayerName` : pseudo | Joueur | Texte [name] |
| `OffsetPosition` : Décaler une position | Position, X [dx] (0), Y [dy] (1), Z [dz] (0) | Position |
| `RandomNumber` : entier au hasard, bornes incluses | Minimum [min] (1), Maximum [max] (6) | Nombre [value] |
| `JoinText` : Joindre des textes | Début [first], Fin [second] | Texte [text] |
| `Add`, `Subtract`, `Multiply` | A [a] (0), B [b] (0) | Nombre [result] |
| `GreaterThan`, `LessThan`, `NumbersEqual` | A, B | Booléen [result] |
| `Not` | Condition [value] (vrai) | Booléen [result] |

### Nœuds de programmation

Leurs broches viennent des définitions du projet. Le nom technique d'une broche qui représente une variable est l'identifiant de cette variable.

| Nœud | Flux | Vise | Broches |
| --- | --- | --- | --- |
| `FunctionEntry` : entrée d'une fonction, d'une procédure ou d'un constructeur | `Start` | | sorties : les paramètres |
| `FunctionReturn` : retour | `End` | | entrées : les valeurs renvoyées (fonction) ou aucune (procédure, constructeur) |
| `CallFunction` : appel | `Step`, ou `Pure` pour une fonction pure | la fonction, procédure ou méthode ; ou l'interface + la méthode | entrées : Objet [target] (méthode non statique, méthode d'interface), puis les paramètres ; sorties : les valeurs renvoyées |
| `NewObject` : créer | `Step` | la classe ou la structure | entrées : paramètres du constructeur (classe) ou champs (structure) ; sortie : l'objet [object] |
| `GetVariable` / `SetVariable` | `Pure` / `Step` | membre : la variable du graphe | sortie la valeur / entrée la nouvelle valeur [value] |
| `GetField` / `SetField` | `Pure` / `Step` | la classe ou structure + le champ | Objet [target], et la valeur [value] |
| `EnumLiteral` : une valeur d'énumération | `Pure` | l'énumération | entrée non reliable [value] ; sortie |
| `EnumEquals` : comparaison | `Pure` | l'énumération | A, B ; Booléen [result] |
| `Self` : Moi | `Pure` | | sortie [self] du type de la classe |
| `SuperConstructor` : constructeur parent | `Step` | la classe parente | entrées : ses paramètres |

Un nœud dont la définition n'existe plus, ou n'est plus visible, devient un nœud « manquant » : sans broches, signalé en erreur, avec un titre qui l'explique (« Appel introuvable », « Champ introuvable »...).

## 6. Le modèle de programme

Le modèle de programme est une vue instantanée de toutes les définitions du projet. Il est reconstruit après chaque modification d'un graphe. Il sait :

- **retrouver** une définition par identifiant, donner son nom (nom affiché, sinon identifiant en jeu) ;
- **décrire** chaque nœud placé dans un graphe (ses broches) ;
- **suivre l'héritage** : ancêtres d'une classe (sans boucler sur un héritage circulaire), sous-types, champs hérités (ceux des parentes d'abord), méthodes héritées (une méthode redéfinie n'apparaît qu'une fois) ;
- **calculer la visibilité** d'une définition ou d'un membre depuis un graphe ;
- **dire quel est « Moi »** dans un graphe : la classe d'un constructeur ou d'une méthode non statique.

### Visibilité

- Une définition est toujours visible depuis elle-même et depuis ce qui lui est intégré.
- **Définition intégrée à une classe** (méthode, type imbriqué) : visible si sa classe l'est, et si son accessibilité le permet.
- **Définition hors de toute classe** : publique, visible partout ; privée (ou protégée), visible seulement depuis les graphes **du même dossier du Dashboard ou d'un de ses sous-dossiers**. Les dossiers servent ainsi de modules.
- **Membre** (champ ou méthode) d'une classe : public, partout ; privé, seulement dans la classe (et ce qui lui est intégré) ; protégé, aussi dans les classes qui en héritent.

### Noms de programme

Les noms saisis sont libres (« Points de vie », « Épée magique »). Ils sont convertis en identifiants de programme :

- **identifiant** : accents retirés, mots séparés par tout caractère qui n'est ni lettre ni chiffre ASCII, premier mot en minuscule initiale, suivants en majuscule initiale, le reste du mot conservé tel quel ; un `_` est ajouté devant un chiffre initial. « Points de vie » donne `pointsDeVie` ; pour un type, `PointsDeVie`.
- **constante** : l'identifiant de type, avec un `_` inséré entre une minuscule et une majuscule, puis tout en majuscules. « Très rare » donne `TRES_RARE`.

Deux noms qui donnent le même identifiant désignent la même chose : c'est ainsi qu'une méthode en redéfinit une autre, et que les doublons sont détectés.

### Ce qu'on peut ajouter dans un graphe (palette)

- Graphe sans corps (structure, interface, énumération) : rien.
- Graphe d'événements : tout le catalogue.
- Fonction, procédure, constructeur : le catalogue sans les événements ni « Annuler l'événement » ; l'entrée (s'il n'y en a pas encore), le retour ; le constructeur parent pour une classe qui a une parente.
- « Moi » quand le graphe s'exécute sur un objet.
- Lire et modifier chaque variable du graphe (graphes d'événements, fonctions, procédures).
- Pour chaque définition visible, triée par nom : appeler (fonction, procédure, chaque méthode d'interface), créer (classe, structure), lire et modifier chaque champ accessible, valeur et comparaison (énumération).

### Création d'un graphe

| Sorte | Identifiant proposé | Nom proposé | Contenu initial |
| --- | --- | --- | --- |
| `Events` | `new_graph` | Nouveau graphe | vide |
| `Function` | `new_function` | Nouvelle fonction | une valeur renvoyée « résultat » (nombre), une entrée reliée à un retour |
| `Procedure` | `new_procedure` | Nouvelle procédure | une entrée |
| `Class` | `new_class` | Nouvelle classe | une entrée (le constructeur) |
| `Struct` | `new_struct` | Nouvelle structure | aucun champ (avertissement tant qu'il n'y en a pas) |
| `Interface` | `new_interface` | Nouvelle interface | aucune méthode |
| `Enum` | `new_enum` | Nouvelle énumération | une valeur « Valeur » |

Une méthode ou un type imbriqué se crée depuis le panneau Définition d'une classe : c'est un graphe ordinaire dont le propriétaire est la classe.

## 7. Validation

La validation d'un graphe produit une liste de problèmes, chacun rattaché à un nœud ou au graphe entier. L'éditeur entoure les nœuds en erreur ; les Diagnostics les listent.

### Règles de la définition

- **Propriétaire** : statique sans propriétaire : erreur. Propriétaire supprimé, qui n'est pas une classe, ou graphe d'événements intégré : erreur. Intégration circulaire : erreur.
- **Noms** : un type sans nom utilisable, ou qui porte le nom d'un type de Java ou du code généré (`Object`, `String`, `Math`, `Integer`, `Boolean`, `System`, `Override`, `Record`, `Enum`, `List`, `Map`, `Player`, `Level`, `BlockPos`, `Item`, `Items`, `ItemStack`, `Block`, `Blocks`, `SoundEvent`, `SoundSource`, `Component`, `ItemEntity`, `InteractionHand`, `BuiltInRegistries`, `ResourceLocation`, `MinecraftForge`, `NeoForge`, `ModItems`, `ModBlocks`, `ModCreativeTabs`, `ModEntities`, `ModSounds`, `ModEntityRenderers`) : erreur. Fonction ou procédure sans nom utilisable : erreur.
- **Doublons** (même identifiant de programme) parmi : les paramètres ; les paramètres et variables d'une fonction ou d'un graphe d'événements ensemble ; les valeurs renvoyées ; les champs ou variables ; les constantes ; les paramètres de chaque méthode d'interface ; les méthodes d'une interface. Nom vide ou sans lettre ni chiffre : erreur.
- Méthode d'interface qui renvoie plus d'une valeur : erreur.
- **Types utilisés** par les variables : type supprimé, ou non visible : erreur ; valeur par défaut d'énumération qui n'existe plus : erreur.
- Fonction sans valeur renvoyée : avertissement. Énumération sans valeur : erreur. Structure sans champ : avertissement.
- **Classe** : parente supprimée, qui n'est pas une classe, circulaire, ou non visible : erreur. Interface implémentée supprimée : erreur. Méthode d'interface sans implémentation de même nom, mêmes types de paramètres et de valeurs renvoyées : erreur. Implémentation non publique : erreur.
- **Redéfinition** (méthode non statique qui porte le nom d'une méthode non statique d'une parente) : signature différente : erreur (« gardez les mêmes paramètres et valeurs renvoyées, ou changez de nom ») ; méthode parente privée : erreur ; accessibilité réduite : erreur.
- **Constructeur** : un « constructeur parent » qui ne suit pas directement l'entrée : erreur ; une parente dont le constructeur a des paramètres sans « constructeur parent » en premier : erreur.

### Règles du corps

- **Graphe d'événements** : aucun événement : avertissement. Entrée, retour ou constructeur parent : erreur.
- **Fonction, procédure, constructeur** : pas d'entrée : erreur ; plus d'une : erreur ; un événement : erreur ; « Annuler l'événement » : erreur.
- **Nœud manquant** : erreur.
- **Entrées** : un fil invalide (selon les règles de branchement) : erreur. Une entrée sans valeur saisissable non reliée et non facultative : erreur. Une valeur saisie invalide : erreur (nombre entier attendu dans les bornes, `true`/`false`, référence d'item, de bloc ou de son valide et qui n'est pas un tag, constante d'énumération existante). L'item d'un événement d'item qui n'est pas un élément du projet : erreur.
- **Atteignabilité** : un nœud qui a une entrée d'exécution mais qu'aucun point de départ n'atteint : avertissement (« ne sera jamais exécuté »).
- **Boucle d'exécution** (un nœud se relance lui-même) : erreur, signalée une fois par nœud.
- **Annuler l'événement** atteint depuis un événement non annulable : erreur.
- **Valeur prise dans le mauvais événement** : un nœud exécuté par l'événement A qui utilise (directement ou à travers des nœuds purs) une sortie de l'événement B : erreur.
- Un événement qui ne déclenche rien : info.

### Ce qui n'est pas encore permis

Pas de boucle (pour, tant que), pas de liste, pas de conversion entre types (« est un », transtypage), pas de nombre décimal, pas de classe ou de méthode abstraite, pas de surcharge (deux méthodes de même nom). Ces manques sont dans la feuille de route ([14](14-feuille-de-route.md)).

## 8. Sémantique d'exécution

Elle découle de la compilation en IR ([07](07-generation-du-mod.md)) et doit rester celle-ci :

- **On suit le fil d'exécution** depuis chaque point de départ. Une action, un appel, une création d'objet ou une affectation devient une instruction ; une condition devient un « si / sinon » dont chaque branche suit son propre fil ; un retour termine le fil. Les fils ne se rejoignent pas : deux branches qui mènent au même nœud le dupliquent dans chacune.
- **Une entrée de valeur** prend, dans l'ordre : le fil branché dessus, sinon la valeur saisie, sinon la valeur par défaut de la broche.
- **Une sortie d'un nœud de départ** est une valeur de l'événement (`player`, `position`) ou un paramètre de la fonction.
- **Une sortie d'un nœud exécuté** (appel, création) est rangée dans une variable locale (`_v1`, `_v2`...), déclarée en tête de méthode et initialisée à la valeur par défaut de son type.
- **Une sortie d'un nœud pur est recalculée à chaque utilisation**, comme dans les Blueprints. Un nœud « Nombre aléatoire » branché deux fois donne donc deux tirages.
- **Un nombre branché sur un texte** est converti en texte.
- **L'entrée « Objet » vide** d'une méthode ou d'un champ désigne « Moi ».
- **Les variables d'un graphe d'événements** sont communes à toutes ses réactions et conservées tant que le jeu tourne. **Les variables globales d'une fonction** sont conservées d'un appel à l'autre ; **ses variables locales** sont réinitialisées à chaque appel.
- **Les réactions d'un graphe d'événements** sont ordonnées par la position de leur événement (de haut en bas, puis de gauche à droite). Une réaction vide n'est pas générée.
