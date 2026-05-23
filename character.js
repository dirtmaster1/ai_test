// Character Data - sprite definitions, character creation, and party initialization
window.CharacterData = {

    resolveActionReference(actionRef, templateGetter) {
        if (!actionRef) {
            return null;
        }

        if (typeof actionRef === 'string') {
            return templateGetter ? templateGetter(actionRef) || null : null;
        }

        if (typeof actionRef === 'object') {
            const baseTemplate = actionRef.id && templateGetter
                ? templateGetter(actionRef.id) || null
                : null;
            return {
                ...(baseTemplate || {}),
                ...actionRef
            };
        }

        return null;
    },

    resolveActionConfigList(actionRefs, resolver, fallbackRefs = []) {
        const refs = Array.isArray(actionRefs)
            ? actionRefs
            : actionRefs
                ? [actionRefs]
                : fallbackRefs;

        return refs
            .map((actionRef) => resolver(actionRef))
            .filter(Boolean)
            .map((action) => ({ ...action }));
    },

    resolveAbilityReference(abilityRef) {
        return this.resolveActionReference(abilityRef, (id) => window.GameData?.getAbilityTemplateById(id));
    },

    resolveSpellReference(spellRef) {
        return this.resolveActionReference(spellRef, (id) => window.GameData?.getSpellTemplateById(id));
    },

    resolveAbilityConfigList(abilityRefs) {
        return this.resolveActionConfigList(abilityRefs, (abilityRef) => this.resolveAbilityReference(abilityRef), ['melee']);
    },

    resolveSpellConfigList(spellRefs) {
        return this.resolveActionConfigList(spellRefs, (spellRef) => this.resolveSpellReference(spellRef));
    },

    getCharacterActionList(character) {
        if (!character) {
            return [];
        }

        return [
            ...(character.abilities ?? []),
            ...(character.spells ?? [])
        ];
    },

    getCharacterActionById(character, actionId = character?.selectedAbilityId) {
        if (!character || !actionId) {
            return null;
        }

        return this.getCharacterActionList(character).find((action) => action.id === actionId) || null;
    },

    resolveEquipmentReference(itemRef) {
        if (!itemRef || typeof itemRef !== 'string') {
            return itemRef;
        }

        const template = window.GameData?.getItemTemplateByRef(itemRef);
        if (!template) {
            return itemRef;
        }

        return template;
    },

    resolveEquipmentConfig(equipmentConfig) {
        if (!equipmentConfig || typeof equipmentConfig !== 'object') {
            return {};
        }

        return Object.entries(equipmentConfig).reduce((acc, [slotKey, itemRef]) => {
            acc[slotKey] = this.resolveEquipmentReference(itemRef);
            return acc;
        }, {});
    },

    createCharacter(config) {
        const strength = config.strength ?? 10;
        const strAbove10 = Math.max(0, strength - 10);
        const strHpBonus = strAbove10;
        const strDamageBonus = Math.floor(strAbove10 / 2);

        const dexterity = config.dexterity ?? 10;
        const dexDiff = dexterity - 10;
        const dexInitiativeBonus = dexDiff;
        const dexRangedDamageBonus = Math.floor(Math.max(0, dexDiff) / 2);

        const intelligence = config.intelligence ?? 10;
        const intAbove10 = Math.max(0, intelligence - 10);
        const intMpRegen = Math.floor(intAbove10 / 2);
        const intMagicDamageBonus = Math.floor(intAbove10 / 2);

        const wisdom = config.wisdom ?? 10;
        const wisAbove10 = Math.max(0, wisdom - 10);
        const healingSpellBonus = Math.floor(wisAbove10 / 2);

        const baseHp = config.hitPoints ?? 10;
        const baseMaxHp = config.maxHitPoints ?? config.hitPoints ?? 10;
        const equipmentConfig = this.resolveEquipmentConfig(config.equipment ?? {});
        const legacyHandsItem = equipmentConfig.hands ?? null;
        const rightHandItem = equipmentConfig.rightHand ?? legacyHandsItem ?? null;
        const leftHandItem = equipmentConfig.leftHand ?? null;
        const {
            hands: _legacyHands,
            rightHand: _configuredRightHand,
            leftHand: _configuredLeftHand,
            ...otherEquipment
        } = equipmentConfig;

        const baseAbilities = this.resolveAbilityConfigList(config.abilities);
        const baseSpells = this.resolveSpellConfigList(config.spells);

        const enhanceActionList = (actions) => actions.map((action) => {
            if (action.type === 'attack' && action.range > 1 && action.damage !== undefined && dexRangedDamageBonus > 0) {
                return { ...action, damage: action.damage + dexRangedDamageBonus };
            }
            if (action.type === 'spell' && action.damage !== undefined && intMagicDamageBonus > 0) {
                return { ...action, damage: action.damage + intMagicDamageBonus };
            }
            if (action.type === 'heal' && action.healAmount !== undefined && healingSpellBonus > 0) {
                return { ...action, healAmount: action.healAmount + healingSpellBonus };
            }
            return action;
        });

        const abilities = enhanceActionList(baseAbilities);
        const spells = enhanceActionList(baseSpells);

        return {
            id: config.id,
            name: config.name,
            level: config.level ?? 1,
            experiencePoints: config.experiencePoints ?? 0,
            role: config.role,
            team: config.team,
            accentColor: config.accentColor,
            pointerColor: config.pointerColor,
            spriteFrame: config.spriteFrame,
            race: config.race ?? 'unknown',
            strength,
            dexterity,
            intelligence,
            wisdom,
            initiative: (config.initiative ?? 10) + dexInitiativeBonus,
            mpRegen: intMpRegen,
            healingSpellBonus,
            gridX: 0,
            gridY: 0,
            mesh: null,
            baseColorHex: 0xffffff,
            facing: 'right',
            directionPointer: null,
            hitPoints: baseHp + strHpBonus,
            maxHitPoints: baseMaxHp + strHpBonus,
            magicPoints: config.magicPoints ?? 0,
            maxMagicPoints: config.maxMagicPoints ?? 0,
            armorClass: config.armorClass ?? 0,
            attackCost: config.attackCost ?? 3,
            maxActionsPerTurn: config.maxActionsPerTurn ?? 5,
            bonusMovement: Math.max(0, Math.floor(config.bonusMovement ?? 0)),
            bonusMovementRemaining: 0,
            actionsRemaining: 0,
            hitAnimEndTime: 0,
            isDead: false,
            fadeFrames: 0,
            removedFromScene: false,
            equipment: {
                head: null,
                body: null,
                rightHand: rightHandItem,
                leftHand: leftHandItem,
                legs: null,
                feet: null,
                neck: null,
                hands: null,
                ...otherEquipment
            },
            abilities,
            spells,
            selectedAbilityId: abilities[0]?.id ?? spells[0]?.id ?? null,
            activeEffects: [],
            pendingLevelUpNotices: []
        };
    },

    getCharacterTilesetPath() {
        return 'character_tileset_1.png';
    },

    getCharacterSpriteFrame(frameId) {
        const imagePath = this.getCharacterTilesetPath();
        const frames = {
            paladin: { imagePath, x: 0, y: 2, width: 132, height: 148 },
            ranger: { imagePath, x: 160, y: 2, width: 121, height: 148 },
            wizard: { imagePath, x: 289, y: 0, width: 130, height: 149 },
            rogue: { imagePath, x: 424, y: 2, width: 114, height: 148 },
            adventurer: { imagePath, x: 550, y: 2, width: 114, height: 148 },
            cleric: { imagePath, x: 674, y: 0, width: 119, height: 151 },
            warrior: { imagePath, x: 5, y: 162, width: 144, height: 139 },
            brawler: { imagePath, x: 154, y: 162, width: 131, height: 138 },
            battlemage: { imagePath, x: 290, y: 161, width: 120, height: 140 },
            shadow: { imagePath, x: 418, y: 162, width: 129, height: 136 },
            shieldKnight: { imagePath, x: 547, y: 161, width: 130, height: 139 },
            stoneGolem: { imagePath, x: 681, y: 162, width: 127, height: 138 },
            goblinBrute: { imagePath, x: 6, y: 311, width: 133, height: 121 },
            goblinArcher: { imagePath, x: 155, y: 308, width: 113, height: 125 },
            goblinWarrior: { imagePath, x: 285, y: 307, width: 125, height: 125 },
            skeletonWarrior: { imagePath, x: 419, y: 307, width: 120, height: 125 },
            orcGuard: { imagePath, x: 546, y: 306, width: 123, height: 127 },
            ghoul: { imagePath, x: 673, y: 309, width: 135, height: 124 },
            giantSpider: { imagePath, x: 5, y: 442, width: 149, height: 95 },
            goblinScout: { imagePath, x: 160, y: 439, width: 112, height: 107 },
            goblinShaman: { imagePath, x: 278, y: 438, width: 132, height: 108 },
            skeletonAdept: { imagePath, x: 415, y: 434, width: 130, height: 113 },
            demonImp: { imagePath, x: 553, y: 437, width: 116, height: 109 },
            specter: { imagePath, x: 674, y: 437, width: 131, height: 111 },
            wolf: { imagePath, x: 3, y: 560, width: 143, height: 106 },
            slime: { imagePath, x: 145, y: 561, width: 136, height: 105 },
            necromancer: { imagePath, x: 284, y: 557, width: 126, height: 119 },
            goblinChieftain: { imagePath, x: 410, y: 556, width: 145, height: 120 },
            ogre: { imagePath, x: 410, y: 556, width: 145, height: 120 },
            drake: { imagePath, x: 563, y: 555, width: 245, height: 121 }
        };

        const frame = frames[frameId];
        return frame ? { ...frame } : null;
    },

    initializeCharacters() {
        this.wizard = this.createCharacter({
            id: 'wizard',
            name: 'Merland',
            role: 'Player',
            team: 'player',
            accentColor: '#4f86ff',
            pointerColor: 0x00eeff,
            spriteFrame: this.getCharacterSpriteFrame('wizard'),
            race: 'human',
            strength: 8,
            dexterity: 9,
            intelligence: 16,
            wisdom: 10,
            hitPoints: 6,
            maxHitPoints: 6,
            magicPoints: 10,
            maxMagicPoints: 10,
            armorClass: 0,
            equipment: {
                body: 'robe',
                hands: 'staff'
            },
            abilities: [
                'staff-strike'
            ],
            spells: [
                'magic-missile'
            ]
        });

        this.warrior = this.createCharacter({
            id: 'warrior',
            name: 'Therin',
            role: 'Player',
            team: 'player',
            accentColor: '#c78a3b',
            pointerColor: 0xffb347,
            spriteFrame: this.getCharacterSpriteFrame('warrior'),
            race: 'dwarf',
            strength: 15,
            dexterity: 11,
            intelligence: 6,
            wisdom: 6,
            armorClass: 3,
            equipment: {
                body: 'chain-mail',
                head: 'steel-helm',
                hands: 'axe'
            },
            abilities: [
                'melee',
                'battle-shout'
            ]
        });

        this.cleric = this.createCharacter({
            id: 'cleric',
            name: 'Elaria',
            role: 'Player',
            team: 'player',
            accentColor: '#c8a84e',
            pointerColor: 0xffe080,
            spriteFrame: this.getCharacterSpriteFrame('cleric'),
            race: 'human',
            strength: 10,
            dexterity: 10,
            intelligence: 12,
            wisdom: 16,
            hitPoints: 8,
            maxHitPoints: 8,
            magicPoints: 8,
            maxMagicPoints: 8,
            armorClass: 2,
            equipment: {
                body: 'chain-mail',
                hands: 'mace'
            },
            abilities: [
                'mace-strike'
            ],
            spells: [
                'lesser-heal',
                'cure-poison'
            ]
        });

        this.ranger = this.createCharacter({
            id: 'ranger',
            name: 'Aragon',
            role: 'Player',
            team: 'player',
            accentColor: '#5bbf7a',
            pointerColor: 0x84ff9f,
            spriteFrame: this.getCharacterSpriteFrame('ranger'),
            race: 'elf',
            strength: 11,
            dexterity: 16,
            intelligence: 10,
            wisdom: 10,
            hitPoints: 8,
            maxHitPoints: 8,
            magicPoints: 4,
            maxMagicPoints: 4,
            armorClass: 1,
            bonusMovement: 2,
            equipment: {
                body: 'leather-armor',
                hands: 'short-bow'
            },
            abilities: [
                'dagger-strike',
                'bow-shot'
            ]
        });

        this.goblin = this.createCharacter({
            id: 'goblin-warrior',
            name: 'Goblin Warrior',
            role: 'AI',
            team: 'ai',
            accentColor: '#d34c4c',
            pointerColor: 0xff6600,
            spriteFrame: this.getCharacterSpriteFrame('goblinWarrior'),
            race: 'goblin',
            strength: 12,
            dexterity: 10,
            intelligence: 4,
            wisdom: 4,
            hitPoints: 8,
            maxHitPoints: 8,
            experiencePoints: 220,
            armorClass: 1,
            abilities: [
                'melee'
            ],
            equipment: {
                hands: 'axe'
            }
        });

        this.goblinArcher = this.createCharacter({
            id: 'goblin-archer',
            name: 'Goblin Archer',
            role: 'AI',
            team: 'ai',
            accentColor: '#72c24e',
            pointerColor: 0xa4ff6a,
            spriteFrame: this.getCharacterSpriteFrame('goblinArcher'),
            race: 'goblin',
            strength: 8,
            dexterity: 12,
            intelligence: 6,
            wisdom: 6,
            hitPoints: 6,
            maxHitPoints: 6,
            experiencePoints: 240,
            magicPoints: 0,
            maxMagicPoints: 0,
            armorClass: 1,
            abilities: [
                'bow-shot'
            ],
            equipment: {
                hands: 'short-bow'
            }
        });

        this.goblinShaman = this.createCharacter({
            id: 'goblin-shaman',
            name: 'Goblin Shaman',
            role: 'AI',
            team: 'ai',
            accentColor: '#9a6fd6',
            pointerColor: 0xd9a5ff,
            spriteFrame: this.getCharacterSpriteFrame('goblinShaman'),
            race: 'goblin',
            strength: 6,
            dexterity: 6,
            intelligence: 12,
            wisdom: 12,
            hitPoints: 5,
            maxHitPoints: 5,
            experiencePoints: 260,
            magicPoints: 10,
            maxMagicPoints: 10,
            armorClass: 0,
            abilities: [],
            spells: [
                'poison-dart',
                'mend-flesh'
            ]
        });

        this.goblinBrute = this.createCharacter({
            id: 'goblin-brute',
            name: 'Goblin Brute',
            role: 'AI',
            team: 'ai',
            accentColor: '#9b3f2a',
            pointerColor: 0xff8855,
            spriteFrame: this.getCharacterSpriteFrame('goblinBrute'),
            race: 'goblin',
            strength: 14,
            dexterity: 6,
            intelligence: 2,
            wisdom: 2,
            hitPoints: 12,
            maxHitPoints: 12,
            experiencePoints: 320,
            armorClass: 2,
            abilities: [
                'melee'
            ],
            equipment: {
                hands: 'axe'
            }
        });

        this.goblinChieftain = this.createCharacter({
            id: 'goblin-chieftain',
            name: 'Goblin Chieftain',
            role: 'AI',
            team: 'ai',
            accentColor: '#d37a2d',
            pointerColor: 0xffc266,
            spriteFrame: this.getCharacterSpriteFrame('goblinChieftain'),
            race: 'goblin',
            strength: 16,
            dexterity: 8,
            intelligence: 6,
            wisdom: 8,
            hitPoints: 24,
            maxHitPoints: 24,
            experiencePoints: 1000,
            armorClass: 3,
            abilities: [
                'melee'
            ],
            equipment: {
                hands: 'chieftain-club'
            }
        });

        this.giantSpider = this.createCharacter({
            id: 'giant-spider',
            name: 'Giant Spider',
            role: 'AI',
            team: 'ai',
            accentColor: '#6f8b5f',
            pointerColor: 0xa8c98f,
            spriteFrame: this.getCharacterSpriteFrame('giantSpider'),
            race: 'spider',
            strength: 6,
            dexterity: 6,
            intelligence: 2,
            wisdom: 4,
            hitPoints: 5,
            maxHitPoints: 5,
            experiencePoints: 250,
            armorClass: 1,
            bonusMovement: 1,
            abilities: [
                'venomous-bite'
            ]
        });

        this.characters = [this.wizard, this.warrior, this.cleric, this.ranger, this.goblin, this.goblinArcher, this.goblinShaman, this.goblinBrute, this.goblinChieftain, this.giantSpider];
        this.playerParty = [this.wizard, this.warrior, this.cleric, this.ranger];
        this.aiParty = [this.goblin, this.goblinArcher, this.goblinShaman, this.goblinBrute, this.goblinChieftain, this.giantSpider];
    }
};
