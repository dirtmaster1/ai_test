// Character Data - sprite definitions, character creation, and party initialization
window.CharacterData = {

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

        const baseAbilities = config.abilities ?? [{ id: 'melee', name: 'Melee Strike', type: 'attack', range: 1, mpCost: 0 }];
        const abilities = baseAbilities.map((ability) => {
            if (ability.type === 'attack' && ability.range > 1 && ability.damage !== undefined && dexRangedDamageBonus > 0) {
                return { ...ability, damage: ability.damage + dexRangedDamageBonus };
            }
            if (ability.type === 'spell' && ability.damage !== undefined && intMagicDamageBonus > 0) {
                return { ...ability, damage: ability.damage + intMagicDamageBonus };
            }
            if (ability.type === 'heal' && ability.healAmount !== undefined && healingSpellBonus > 0) {
                return { ...ability, healAmount: ability.healAmount + healingSpellBonus };
            }
            return ability;
        });

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
            meleeAttackDamage: (config.meleeAttackDamage ?? 5) + strDamageBonus,
            armorClass: config.armorClass ?? 0,
            attackCost: config.attackCost ?? 3,
            maxActionsPerTurn: config.maxActionsPerTurn ?? 5,
            actionsRemaining: 0,
            hitAnimEndTime: 0,
            isDead: false,
            fadeFrames: 0,
            removedFromScene: false,
            equipment: {
                head: null,
                body: null,
                hands: null,
                legs: null,
                feet: null,
                neck: null,
                ...(config.equipment ?? {})
            },
            abilities,
            selectedAbilityId: abilities[0].id
        };
    },

    getCharacterTilesetPath() {
        return 'character_tileset_1.png';
    },

    getCharacterSpriteFrame(frameId) {
        const imagePath = this.getCharacterTilesetPath();
        const frames = {
            wizard: { imagePath, x: 289, y: 0, width: 130, height: 149 },
            dwarf: { imagePath, x: 5, y: 162, width: 144, height: 139 },
            cleric: { imagePath, x: 674, y: 0, width: 119, height: 151 },
            ranger: { imagePath, x: 160, y: 2, width: 121, height: 148 },
            goblinWarrior: { imagePath, x: 285, y: 307, width: 125, height: 125 },
            goblinArcher: { imagePath, x: 155, y: 308, width: 113, height: 125 },
            goblinShaman: { imagePath, x: 278, y: 438, width: 132, height: 108 },
            goblinBrute: { imagePath, x: 6, y: 311, width: 133, height: 121 }
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
            meleeAttackDamage: 4,
            armorClass: 0,
            equipment: {
                body: 'Robe',
                hands: '2H Staff (4 DMG)'
            },
            abilities: [
                { id: 'melee', name: 'Staff Strike', type: 'attack', range: 1, mpCost: 0 },
                { id: 'magic-missile', name: 'Magic Missile', type: 'spell', range: 5, mpCost: 5, damage: 4 }
            ]
        });

        this.dwarf = this.createCharacter({
            id: 'dwarf-warrior',
            name: 'Therin',
            role: 'Player',
            team: 'player',
            accentColor: '#c78a3b',
            pointerColor: 0xffb347,
            spriteFrame: this.getCharacterSpriteFrame('dwarf'),
            race: 'dwarf',
            strength: 15,
            dexterity: 11,
            intelligence: 6,
            wisdom: 6,
            meleeAttackDamage: 6,
            armorClass: 3,
            equipment: {
                body: 'Chain Mail (+2 AC)',
                head: 'Steel Helm (+1 AC)',
                hands: '1H Axe (6 DMG)'
            },
            abilities: [
                { id: 'melee', name: 'Melee Strike', type: 'attack', range: 1, mpCost: 0 },
                { id: 'battle-shout', name: 'Battle Shout', type: 'buff', range: 3, mpCost: 0, acBonus: 1 }
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
            meleeAttackDamage: 6,
            armorClass: 2,
            equipment: {
                body: 'Chain Mail (+2 AC)',
                hands: '1H Mace (6 DMG)'
            },
            abilities: [
                { id: 'mace-strike', name: 'Mace Strike', type: 'attack', range: 1, mpCost: 0 },
                { id: 'heal', name: 'Holy Heal', type: 'heal', range: 5, mpCost: 4, healAmount: 5 }
            ]
        });

        this.ranger = this.createCharacter({
            id: 'ranger-aragon',
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
            meleeAttackDamage: 4,
            armorClass: 1,
            equipment: {
                body: 'Leather Armor (+1 AC)',
                hands: '2H Shortbow (6 DMG)'
            },
            abilities: [
                { id: 'dagger-strike', name: 'Dagger Strike', type: 'attack', range: 1, mpCost: 0, damage: 4 },
                { id: 'bow-shot', name: 'Bow Shot', type: 'attack', range: 5, mpCost: 0, damage: 6 }
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
            armorClass: 1
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
            meleeAttackDamage: 4,
            armorClass: 1,
            abilities: [
                { id: 'bow-shot', name: 'Bow Shot', type: 'attack', range: 4, mpCost: 0, damage: 4 }
            ]
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
            meleeAttackDamage: 3,
            armorClass: 0,
            abilities: [
                { id: 'heal', name: 'Mend Flesh', type: 'heal', range: 3, mpCost: 3, healAmount: 4 },
                { id: 'inflict-pain', name: 'Inflict Pain', type: 'buff', range: 2, mpCost: 4, damageBonus: 1 }
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
            meleeAttackDamage: 6,
            armorClass: 2
        });

        this.characters = [this.wizard, this.dwarf, this.cleric, this.ranger, this.goblin, this.goblinArcher, this.goblinShaman, this.goblinBrute];
        this.playerParty = [this.wizard, this.dwarf, this.cleric, this.ranger];
        this.aiParty = [this.goblin, this.goblinArcher, this.goblinShaman, this.goblinBrute];
    }
};
