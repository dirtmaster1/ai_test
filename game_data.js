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
            appliesToAbilityId: 'melee',
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
        melee: { id: 'melee', name: 'Melee Strike', type: 'attack', range: 1, mpCost: 0 },
        'wolf-bite': { id: 'wolf-bite', name: 'Bite', type: 'attack', range: 1, mpCost: 0, damage: 5 },
        'venomous-bite': { id: 'venomous-bite', name: 'Venomous Bite', type: 'attack', range: 1, mpCost: 0, damage: 4 },
        'staff-strike': { id: 'staff-strike', name: 'Staff Strike', type: 'attack', range: 1, mpCost: 0 },
        'mace-strike': { id: 'mace-strike', name: 'Mace Strike', type: 'attack', range: 1, mpCost: 0 },
        'dagger-strike': { id: 'dagger-strike', name: 'Dagger Strike', type: 'attack', range: 1, mpCost: 0, damage: 4 },
        'bow-shot': { id: 'bow-shot', name: 'Bow Shot', type: 'attack', range: 5, mpCost: 0, damage: 6, projectileAnimation: 'arrow', resolveOnImpact: true },
        'goblin-bow-shot': { id: 'goblin-bow-shot', name: 'Bow Shot', type: 'attack', range: 4, mpCost: 0, damage: 4, projectileAnimation: 'arrow', resolveOnImpact: true },
        'battle-shout': { id: 'battle-shout', name: 'Battle Shout', type: 'buff', range: 3, mpCost: 0, damageBonus: 1, duration: 2 },
        charge: { id: 'charge', name: 'Charge', type: 'move', range: 5, mpCost: 0, actionCost: 3, targetMode: 'cell' }
    },

    SPELL_DEFS: {
        'call-of-the-wolf': { id: 'call-of-the-wolf', name: 'Call of the Wolf', type: 'spell', range: 0, mpCost: 4, actionCost: 3 },
        'magic-missile': { id: 'magic-missile', name: 'Magic Missile', type: 'spell', range: 5, mpCost: 5, damage: 4, projectileAnimation: 'magic-missile', resolveOnImpact: true },
        'poison-dart': { id: 'poison-dart', name: 'Poison Dart', type: 'spell', range: 5, mpCost: 5 },
        sleep: { id: 'sleep', name: 'Sleep', type: 'spell', range: 4, mpCost: 7, targetMode: 'cell', duration: 2, radius: 1 },
        'lesser-heal': { id: 'lesser-heal', name: 'Lesser Heal', type: 'heal', range: 5, mpCost: 4, healAmount: 5 },
        'mend-flesh': { id: 'mend-flesh', name: 'Mend Flesh', type: 'heal', range: 3, mpCost: 3, healAmount: 4 },
        'inflict-pain': { id: 'inflict-pain', name: 'Inflict Pain', type: 'buff', range: 2, mpCost: 4, damageBonus: 1, duration: 2 },
        'blessing': { id: 'blessing', name: 'Blessing', type: 'buff', range: 3, mpCost: 5, acBonus: 1, duration: 3 },
        'cure-poison': { id: 'cure-poison', name: 'Cure Poison', type: 'heal', range: 3, mpCost: 3, curePoison: true },
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
