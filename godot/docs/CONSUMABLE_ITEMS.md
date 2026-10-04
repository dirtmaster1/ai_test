# Consumable Items

Party units have two consumable slots. Equip potions from the shared party
inventory in the inventory menu; equipped items appear above the ability bar.
Selecting an equipped slot and choosing **Unequip** returns it to shared
inventory.

Consumables can be used in combat or exploration. In combat, using one spends
the unit's action for the turn. In exploration, use is free. Healing and magic
restoration stop at the unit's maximum HP and MP; a potion with no effect
remains equipped and is not consumed.

Consumables are defined in `resources/game_data.json` with `type: "potion"`,
`slot: "consumable"`, and a `use_effect` containing an effect type and amount.
The initial effects are `restore_hit_points` and `restore_magic_points`.
Mira the Curio Trader stocks one of each potion.

Key items use `type: "key"` and a `key_id`. They remain in the party's shared
inventory, cannot be equipped or used, and are not consumed when used to unlock
a matching keyed door. To place one in a chest, add its item ID to that chest's
`loot_item_ids` marker property.
