// Game Data - master dictionaries for items, abilities, and spells
window.GameData = {
    ITEM_DEFS: {
        'small-shield': {
            id: 'small-shield',
            name: 'Small Shield',
            slot: 'hands',
            handType: '1H',
            type: 'armor',
            modifiers: { armorClass: 1 },
            accentColor: '#9fd1ff'
        },
        'long-bow': {
            id: 'long-bow',
            name: 'Long Bow',
            slot: 'hands',
            handType: '2H',
            type: 'weapon',
            appliesToAbilityId: 'bow-shot',
            modifiers: { attackDamage: 6, attackRange: 6 },
            accentColor: '#9ee39a'
        },
        'short-bow': {
            id: 'short-bow',
            name: 'Short Bow',
            slot: 'hands',
            handType: '2H',
            type: 'weapon',
            appliesToAbilityId: 'bow-shot',
            modifiers: { attackDamage: 5, attackRange: 5 },
            accentColor: '#9bcf86'
        },
        staff: {
            id: 'staff',
            name: 'Staff',
            slot: 'hands',
            handType: '2H',
            type: 'weapon',
            appliesToAbilityId: 'staff-strike',
            modifiers: { attackDamage: 4 },
            accentColor: '#bda7db'
        },
        mace: {
            id: 'mace',
            name: 'Mace',
            slot: 'hands',
            handType: '1H',
            type: 'weapon',
            appliesToAbilityId: 'mace-strike',
            modifiers: { attackDamage: 6 },
            accentColor: '#d7c28f'
        },
        axe: {
            id: 'axe',
            name: 'Axe',
            slot: 'hands',
            handType: '1H',
            type: 'weapon',
            appliesToAbilityId: 'melee',
            modifiers: { attackDamage: 6 },
            accentColor: '#d9b08c'
        },
        'chieftain-club': {
            id: 'chieftain-club',
            name: 'Chieftain Club',
            slot: 'hands',
            handType: '2H',
            type: 'weapon',
            appliesToAbilityId: 'melee',
            modifiers: { attackDamage: 8, strength: 2 },
            accentColor: '#f0d28b'
        },
        robe: {
            id: 'robe',
            name: 'Robe',
            slot: 'body',
            type: 'armor',
            modifiers: {},
            accentColor: '#b6a8cf'
        },
        'chain-mail': {
            id: 'chain-mail',
            name: 'Chain Mail',
            slot: 'body',
            type: 'armor',
            modifiers: { armorClass: 2 },
            accentColor: '#d5c2a1'
        },
        'leather-armor': {
            id: 'leather-armor',
            name: 'Leather Armor',
            slot: 'body',
            type: 'armor',
            modifiers: { armorClass: 1 },
            accentColor: '#b98c62'
        },
        'steel-helm': {
            id: 'steel-helm',
            name: 'Steel Helm',
            slot: 'head',
            type: 'armor',
            modifiers: { armorClass: 1 },
            accentColor: '#c6d0da'
        },
        'mages-amulet': {
            id: 'mages-amulet',
            name: 'Mages Amulet',
            slot: 'neck',
            type: 'armor',
            modifiers: { spellDamage: 1 },
            accentColor: '#b7b7ff'
        },
        'healers-circlet': {
            id: 'healers-circlet',
            name: 'Healers Circlet',
            slot: 'head',
            type: 'armor',
            modifiers: { healingBonus: 2 },
            accentColor: '#ffd2a8'
        }
    },

    ITEM_REF_ALIASES: {
        'small shield': 'small-shield',
        'long bow': 'long-bow',
        'short bow': 'short-bow',
        'chieftain club': 'chieftain-club',
        'chain mail': 'chain-mail',
        'leather armor': 'leather-armor',
        'steel helm': 'steel-helm',
        'mages amulet': 'mages-amulet',
        'healers circlet': 'healers-circlet'
    },

    EQUIPMENT_LOOT_ITEM_IDS: ['small-shield', 'long-bow', 'mages-amulet', 'healers-circlet'],

    ABILITY_DEFS: {
        melee: { id: 'melee', name: 'Melee Strike', type: 'attack', mpCost: 0, weaponDriven: true },
        'wolf-bite': { id: 'wolf-bite', name: 'Bite', type: 'attack', range: 1, mpCost: 0, damage: 5 },
        'venomous-bite': { id: 'venomous-bite', name: 'Venomous Bite', type: 'attack', range: 1, mpCost: 0, damage: 4 },
        'skeletal-slash': { id: 'skeletal-slash', name: 'Skeletal Slash', type: 'attack', range: 1, mpCost: 0, damage: 6 },
        'staff-strike': { id: 'staff-strike', name: 'Staff Strike', type: 'attack', mpCost: 0, weaponDriven: true },
        'mace-strike': { id: 'mace-strike', name: 'Mace Strike', type: 'attack', mpCost: 0, weaponDriven: true },
        'dagger-strike': { id: 'dagger-strike', name: 'Dagger Strike', type: 'attack', mpCost: 0, weaponDriven: true },
        'bow-shot': { id: 'bow-shot', name: 'Bow Shot', type: 'attack', mpCost: 0, weaponDriven: true, projectileAnimation: 'arrow', resolveOnImpact: true },
        'goblin-bow-shot': { id: 'goblin-bow-shot', name: 'Bow Shot', type: 'attack', range: 4, mpCost: 0, damage: 4, projectileAnimation: 'arrow', resolveOnImpact: true },
        'battle-shout': { id: 'battle-shout', name: 'Battle Shout', type: 'buff', range: 3, mpCost: 0, damageBonus: 1, duration: 2 },
        charge: { id: 'charge', name: 'Charge', type: 'move', range: 5, mpCost: 0, actionCost: 3, targetMode: 'cell' }
    },

    SPELL_DEFS: {
        'call-of-the-wolf': { id: 'call-of-the-wolf', name: 'Call of the Wolf', type: 'spell', range: 0, mpCost: 4, actionCost: 3 },
        'raise-undead': { id: 'raise-undead', name: 'Raise Undead', type: 'spell', range: 0, mpCost: 6, actionCost: 3, summonCount: 2 },
        'magic-missile': { id: 'magic-missile', name: 'Magic Missile', type: 'spell', range: 5, mpCost: 5, damage: 4, projectileAnimation: 'magic-missile', resolveOnImpact: true },
        'grave-chill': { id: 'grave-chill', name: 'Grave Chill', type: 'spell', range: 4, mpCost: 4, damage: 5, projectileAnimation: 'magic-missile', resolveOnImpact: true },
        'poison-dart': { id: 'poison-dart', name: 'Poison Dart', type: 'spell', range: 5, mpCost: 5 },
        sleep: { id: 'sleep', name: 'Sleep', type: 'spell', range: 4, mpCost: 7, targetMode: 'cell', duration: 2, radius: 1 },
        'lesser-heal': { id: 'lesser-heal', name: 'Lesser Heal', type: 'heal', range: 5, mpCost: 4, healAmount: 5 },
        'mend-flesh': { id: 'mend-flesh', name: 'Mend Flesh', type: 'heal', range: 3, mpCost: 3, healAmount: 4 },
        'inflict-pain': { id: 'inflict-pain', name: 'Inflict Pain', type: 'buff', range: 2, mpCost: 4, damageBonus: 1, duration: 2 },
        'blessing': { id: 'blessing', name: 'Blessing', type: 'buff', range: 3, mpCost: 5, acBonus: 1, duration: 3 },
        'cure-poison': { id: 'cure-poison', name: 'Cure Poison', type: 'heal', range: 3, mpCost: 3, curePoison: true },
    },

    DUNGEON_PROPS_TILESET_PATH: 'game_assets/dungeon_props_1.png',

    DUNGEON_PROP_SPRITE_FRAMES: {
        steelCage: { name: 'Steel Cage', x: 1328, y: 278, width: 113, height: 142 },
        alchemyDeskLarge: { name: 'Alchemy Desk', x: 511, y: 443, width: 169, height: 137 },
        alchemyDeskSmall: { name: 'Alchemy Worktable', x: 344, y: 441, width: 153, height: 144 },
        altar: { name: 'Altar', x: 683, y: 457, width: 179, height: 127 },
        barrel: { name: 'Barrel', x: 1167, y: 1, width: 116, height: 128 },
        bed: { name: 'Bed', x: 519, y: 5, width: 162, height: 124 },
        bookshelf: { name: 'Bookshelf', x: 1016, y: 0, width: 135, height: 128 },
        brazierGold: { name: 'Golden Brazier', x: 1007, y: 153, width: 145, height: 111 },
        brazierStone: { name: 'Stone Brazier', x: 1320, y: 745, width: 128, height: 120 },
        butcherTable: { name: 'Butcher Table', x: 197, y: 769, width: 151, height: 96 },
        cauldron: { name: 'Cauldron', x: 693, y: 1, width: 139, height: 134 },
        skullPile: { name: 'Skull Pile', x: 678, y: 753, width: 175, height: 112 },
        benchCandle: { name: 'Candle Bench', x: 0, y: 1, width: 176, height: 127 },
        tableSkull: { name: 'Skull Table', x: 352, y: 1, width: 147, height: 121 },
        chair: { name: 'Chair', x: 1310, y: 1, width: 122, height: 129 },
        chestClosedIron: { name: 'Iron Chest', x: 8, y: 153, width: 161, height: 112 },
        chestClosedGold: { name: 'Gold Chest', x: 195, y: 153, width: 149, height: 112 },
        chestOpenGold: { name: 'Opened Gold Chest', x: 356, y: 153, width: 142, height: 112 },
        chestClosedSteel: { name: 'Steel Chest', x: 515, y: 153, width: 165, height: 112 },
        barrels: { name: 'Barrel Stack', x: 8, y: 761, width: 173, height: 104 },
        coinPile: { name: 'Coin Pile', x: 1160, y: 158, width: 152, height: 98 },
        crate: { name: 'Crate', x: 692, y: 159, width: 140, height: 99 },
        stoneDebris1: { name: 'Stone Debris', x: 1016, y: 760, width: 139, height: 105 },
        forge: { name: 'Forge', x: 863, y: 622, width: 141, height: 106 },
        altarCandle: { name: 'Candle Altar', x: 680, y: 609, width: 176, height: 120 },
        weaponRack3: { name: 'Weapon Rack', x: 1328, y: 457, width: 137, height: 121 },
        campfire1: { name: 'Campfire', x: 1000, y: 576, width: 160, height: 144 },
        weaponRack2: { name: 'Weapon Rack', x: 1310, y: 596, width: 185, height: 144 },
        pot: { name: 'Pot', x: 1320, y: 153, width: 135, height: 108 },
        campfireLarge: { name: 'Large Campfire', x: 7, y: 293, width: 174, height: 125 },
        campfireTallLeft: { name: 'Tall Campfire', x: 190, y: 289, width: 156, height: 132 },
        campfireTallRight: { name: 'Tall Campfire', x: 356, y: 289, width: 150, height: 130 },
        candleDeskLeft: { name: 'Candle Desk', x: 1000, y: 289, width: 160, height: 143 },
        candleDeskRight: { name: 'Candle Desk', x: 1160, y: 290, width: 160, height: 137 },
        tableCandles: { name: 'Candle Table', x: 843, y: 288, width: 149, height: 137 },
        roundTable: { name: 'Round Table', x: 189, y: 1, width: 147, height: 120 },
        sack: { name: 'Sack', x: 853, y: 150, width: 132, height: 115 },
        sarcophagus: { name: 'Sarcophagus', x: 366, y: 761, width: 146, height: 104 },
        campfire2: { name: 'Campfire', x: 1161, y: 764, width: 151, height: 101 },
        stoneAltar: { name: 'Stone Altar', x: 863, y: 456, width: 144, height: 122 },
        supplyPile: { name: 'Supply Pile', x: 519, y: 615, width: 154, height: 115 },
        weaponRack1: { name: 'Weapon Rack', x: 840, y: 1, width: 163, height: 127 },
        spikeTrap1: { name: 'Spike Trap', x: 520, y: 292, width: 160, height: 137 },
        spikeTrap2: { name: 'Spike Trap', x: 695, y: 302, width: 160, height: 129 },
        stoneDebris2: { name: 'Stone Debris', x: 358, y: 610, width: 154, height: 119 },
        stoneUrn: { name: 'Stone Urn', x: 859, y: 753, width: 149, height: 112 },
        tombstone: { name: 'Tombstone', x: 859, y: 753, width: 149, height: 112 },
        torch: { name: 'Torch', x: 232, y: 442, width: 79, height: 143 },
        barrels1: { name: 'Barrels', x: 7, y: 617, width: 176, height: 119 },
        treasureStack: { name: 'Treasure Stack', x: 524, y: 761, width: 148, height: 104 },
        barrels2: { name: 'Barrels', x: 8, y: 441, width: 186, height: 145 },
        workbench: { name: 'Workbench', x: 195, y: 608, width: 162, height: 128 }
    },

    DUNGEON_PROP_DEFAULTS: {
        bed: { roomTheme: 'barracks', searchable: false },
        tableCandles: { roomTheme: 'barracks', searchable: false },
        weaponRack1: { roomTheme: 'armory', searchable: true },
        weaponRack2: { roomTheme: 'armory', searchable: true },
        weaponRack3: { roomTheme: 'armory', searchable: true },
        chestClosedIron: { roomTheme: 'treasure', searchable: true },
        chestClosedSteel: { roomTheme: 'treasure', searchable: true },
        chestClosedGold: { roomTheme: 'treasure', searchable: true },
        barrels1: { roomTheme: 'storage', searchable: true },
        barrels2: { roomTheme: 'storage', searchable: true },
        barrel: { roomTheme: 'storage', searchable: true },
        crate: { roomTheme: 'storage', searchable: true },
        spikeTrap1: { roomTheme: 'trap', searchable: false },
        spikeTrap2: { roomTheme: 'trap', searchable: false }
    },

    DUNGEON_PROP_THEME_POOLS: {
        barracks: ['bed', 'chestClosedIron', 'tableCandles', 'chair', 'chair'],
        armory: ['weaponRack1', 'weaponRack2', 'weaponRack3', 'chestClosedSteel'],
        storage: ['crate', 'barrels1', 'barrel', 'barrels2', 'chestClosedGold']
    },

    DUNGEON_TRAP_PROP_IDS: ['spikeTrap1', 'spikeTrap2'],

    DUNGEON_PROP_LOOT_GROUPS: {
        chest: ['chestClosedIron', 'chestClosedGold', 'chestClosedSteel'],
        rack: ['weaponRack1', 'weaponRack2', 'weaponRack3'],
        storage: ['crate', 'barrel', 'barrels1', 'barrels2']
    },

    getDungeonPropsTilesetPath() {
        return this.DUNGEON_PROPS_TILESET_PATH;
    },

    getDungeonPropSpriteFrames() {
        const imagePath = this.getDungeonPropsTilesetPath();
        return Object.entries(this.DUNGEON_PROP_SPRITE_FRAMES).reduce((frames, [frameId, frame]) => {
            frames[frameId] = { imagePath, ...frame };
            return frames;
        }, {});
    },

    getDungeonPropDisplayName(frameId) {
        if (!frameId) {
            return 'Unknown Prop';
        }

        return this.DUNGEON_PROP_SPRITE_FRAMES?.[frameId]?.name || frameId;
    },

    getDungeonPropDefaults(frameId) {
        if (!frameId) {
            return null;
        }

        const defaults = this.DUNGEON_PROP_DEFAULTS[frameId];
        return defaults ? { ...defaults } : null;
    },

    createDungeonPropConfig(frameId, overrides = {}) {
        if (!frameId) {
            return null;
        }

        const defaults = this.getDungeonPropDefaults(frameId) || {};
        const config = {
            frameId,
            name: this.getDungeonPropDisplayName(frameId),
            roomTheme: defaults.roomTheme || 'custom',
            searchable: Boolean(defaults.searchable),
            ...overrides
        };

        config.searchable = Boolean(config.searchable);
        return config;
    },

    createDungeonPropLegendFromTokens(tokenConfig = {}) {
        return Object.entries(tokenConfig).reduce((legend, [token, configOrId]) => {
            const normalizedConfig = typeof configOrId === 'string'
                ? { propId: configOrId }
                : (configOrId || {});
            const { propId, ...overrides } = normalizedConfig;
            const entry = this.createDungeonPropConfig(propId, overrides);
            if (entry) {
                legend[token] = entry;
            }
            return legend;
        }, {});
    },

    getDungeonPropThemePool(theme) {
        const pool = this.DUNGEON_PROP_THEME_POOLS[theme] || [];
        return [...pool];
    },

    getDungeonTrapPropIds() {
        return [...this.DUNGEON_TRAP_PROP_IDS];
    },

    isDungeonPropInLootGroup(frameId, groupName) {
        if (!frameId || !groupName) {
            return false;
        }

        const group = this.DUNGEON_PROP_LOOT_GROUPS[groupName] || [];
        return group.includes(frameId);
    },

    getDungeonPropLootProfile(frameId) {
        if (this.isDungeonPropInLootGroup(frameId, 'chest')) {
            return { chance: 0.9, minGold: 4, maxGold: 14, equipmentDropChance: 0.5 };
        }

        if (this.isDungeonPropInLootGroup(frameId, 'rack')) {
            return { chance: 0.55, minGold: 2, maxGold: 8, equipmentDropChance: 0.1 };
        }

        if (this.isDungeonPropInLootGroup(frameId, 'storage')) {
            return { chance: 0.65, minGold: 1, maxGold: 7, equipmentDropChance: 0.1 };
        }

        return { chance: 0.35, minGold: 1, maxGold: 4, equipmentDropChance: 0.1 };
    },

    normalizeRefKey(ref) {
        return String(ref || '')
            .toLowerCase()
            .replace(/\([^)]*\)/g, '')
            .replace(/[^a-z0-9]+/g, ' ')
            .trim();
    },

    cloneTemplate(template) {
        if (!template || typeof template !== 'object') {
            return null;
        }

        return {
            ...template,
            modifiers: template.modifiers ? { ...template.modifiers } : undefined
        };
    },

    getItemTemplateById(id) {
        if (!id) {
            return null;
        }

        return this.cloneTemplate(this.ITEM_DEFS[id] || null);
    },

    getItemTemplateByRef(ref) {
        const normalized = this.normalizeRefKey(ref);
        if (!normalized) {
            return null;
        }

        const directId = normalized.replace(/\s+/g, '-');
        if (this.ITEM_DEFS[directId]) {
            return this.getItemTemplateById(directId);
        }

        const aliasedId = this.ITEM_REF_ALIASES[normalized];
        if (aliasedId && this.ITEM_DEFS[aliasedId]) {
            return this.getItemTemplateById(aliasedId);
        }

        return null;
    },

    getAbilityTemplateById(id) {
        if (!id) {
            return null;
        }

        return this.cloneTemplate(this.ABILITY_DEFS[id] || null);
    },

    getSpellTemplateById(id) {
        if (!id) {
            return null;
        }

        return this.cloneTemplate(this.SPELL_DEFS[id] || null);
    },

    getAbilityOrSpellTemplateById(id) {
        return this.getAbilityTemplateById(id) || this.getSpellTemplateById(id) || null;
    }
};
