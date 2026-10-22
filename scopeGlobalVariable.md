# Scopes

pas de traduction convention toujours xxx_xxx

## Player

| Type                              | À quoi ça sert ?                                                         |
| --------------------------------- | ------------------------------------------------------------------------ |
| `player_persistent`               | Une valeur propre au joueur, conservée après sa mort                     |
| `player_session`                  | Une valeur propre au joueur, conservée seulement pdt session             |
| `player_lifetime`                 | Une valeur propre au joueur, réinitialisée à sa mort                     |
| `player_dimension`                | Une valeur propre au joueur, dans une dimension                          |
| `player_account`                  | Une valeur propre au compte du joueur                                    |
| `player_guild`                    | Une valeur propre au joueur, partagée par une guilde                     |
| `player_team`                     | Une valeur partagée par les joueurs d'une équipe                         |

## Entity

| Type                              | À quoi ça sert ?                                                         |
| --------------------------------- | ------------------------------------------------------------------------ |
| `entity_type`                     | Une valeur commune à tous les mobs d'un même type                        |
| `entity_owner`                    | Une valeur liée au propriétaire d'une entité                             |
| `entity_group`                    | Une valeur partagée par un groupe d'entités                              |
| `entity_dimension`                | Une valeur d'une entité, dans une dimension                              |

## Global

| Type                              | À quoi ça sert ?                                                         |
| --------------------------------- | ------------------------------------------------------------------------ |
| `global_map`                      | Une valeur commune à toute la sauvegarde                                 |
| `global_dimension`                | Une valeur liée à une dimension                                          |
| `global_session`                  | Une valeur temporaire, perdue à la fermeture du jeu                      |
| `global_chunk`                    | Une valeur liée à un chunk                                               |
| `global_server`                   | Une valeur commune à tout le serveur                                     |

## world

| Type                              | À quoi ça sert ?                                                         |
| --------------------------------- | ------------------------------------------------------------------------ |
| `world_structure`                 | Une valeur commune à une structure                                       |
| `world_biome`                     | Une valeur liée à un biome                                               |
| `world_dimension`                 | Une valeur liée à une dimension                                          |
