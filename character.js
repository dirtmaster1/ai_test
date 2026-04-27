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
            spriteRows: config.spriteRows,
            portraitLabel: config.portraitLabel,
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

    initializeCharacters() {
        this.wizard = this.createCharacter({
            id: 'wizard',
            name: 'Merland',
            role: 'Player',
            team: 'player',
            accentColor: '#4f86ff',
            pointerColor: 0x00eeff,
            spriteRows: this.getWizardSpriteRows(),
            portraitLabel: 'WZ',
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
            spriteRows: this.getDwarfSpriteRows(),
            portraitLabel: 'DW',
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
            spriteRows: this.getClericSpriteRows(),
            portraitLabel: 'CL',
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
            spriteRows: this.getRangerSpriteRows(),
            portraitLabel: 'AR',
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
            spriteRows: this.getGoblinSpriteRows(),
            portraitLabel: 'GB',
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
            spriteRows: this.getGoblinArcherSpriteRows(),
            portraitLabel: 'GA',
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
            spriteRows: this.getGoblinShamanSpriteRows(),
            portraitLabel: 'GS',
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
            spriteRows: this.getGoblinBruteSpriteRows(),
            portraitLabel: 'GB',
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
    },

    getWizardSpriteRows() {
        const _ = null;
        const DH = '#0a2f7a';
        const LH = '#1a5cc8';
        const GS = '#ffd700';
        const SK = '#ffbb88';
        const EY = '#1a0800';
        const WB = '#d0d0d0';
        const RB = '#1050b0';
        const RL = '#3a7ee0';
        const ST = '#5d3a28';
        return [
            [_, _, _, _, _, _, DH, DH, DH, _, _, _, _, _, _, _],
            [_, _, _, _, _, DH, DH, LH, DH, DH, _, _, _, _, _, _],
            [_, _, _, _, DH, DH, LH, DH, DH, DH, DH, _, _, _, _, _],
            [_, _, _, DH, DH, GS, DH, DH, DH, DH, DH, _, _, _, _, _],
            [_, _, DH, DH, DH, DH, DH, DH, DH, DH, DH, DH, _, _, _, _],
            [_, _, DH, DH, LH, DH, DH, DH, DH, LH, DH, DH, _, _, _, _],
            [_, _, _, SK, SK, SK, SK, SK, SK, SK, SK, _, _, _, _, _],
            [_, _, _, SK, EY, SK, SK, EY, SK, SK, SK, _, _, _, _, _],
            [_, _, _, SK, SK, SK, SK, SK, SK, SK, SK, _, _, _, _, _],
            [_, ST, WB, WB, WB, WB, WB, WB, WB, WB, WB, _, _, _, _, _],
            [_, ST, WB, RB, RB, RB, RB, RB, RB, RB, WB, _, _, _, _, _],
            [_, ST, WB, RB, RL, RB, RB, RL, RB, RB, WB, _, _, _, _, _],
            [GS, ST, WB, RB, RB, RB, RB, RB, RB, RB, WB, _, _, _, _, _],
            [_, _, WB, RB, RB, RB, RB, RB, RB, RB, WB, _, _, _, _, _],
            [_, _, WB, WB, RB, RB, RB, RB, RB, WB, WB, _, _, _, _, _],
            [_, _, _, WB, WB, RB, RB, RB, WB, WB, _, _, _, _, _, _]
        ];
    },

    getDwarfSpriteRows() {
        const _ = null;
        const HM = '#5f6878';
        const HH = '#9ea8ba';
        const SK = '#d8a272';
        const EY = '#170d08';
        const BR = '#6b3c1d';
        const BH = '#b0682f';
        const AR = '#6d4c34';
        const AH = '#9b7854';
        const ST = '#3d2718';
        const AX = '#cfd5de';
        const SH = '#818998';
        const BT = '#21150f';
        return [
            [_, _, _, _, _, _, HH, HH, HH, _, _, _, _, _, _, _],
            [_, _, _, _, _, HM, HM, HH, HM, HM, _, _, _, _, _, _],
            [_, _, _, _, HM, HM, HM, HM, HM, HM, HM, _, _, _, _, _],
            [_, _, _, HM, HH, HM, HM, HM, HM, HH, HM, _, _, _, _, _],
            [_, _, HM, HM, HM, HM, HM, HM, HM, HM, HM, HM, _, _, _, _],
            [_, _, _, SK, SK, SK, SK, SK, SK, SK, SK, _, _, _, _, _],
            [_, _, _, SK, EY, SK, BH, BH, SK, EY, SK, _, _, _, _, _],
            [_, _, _, SK, BH, BH, BR, BR, BH, BH, SK, _, _, _, _, _],
            [_, AX, SH, BR, BR, BR, BR, BR, BR, BR, BR, _, _, _, _, _],
            [_, ST, AR, AR, AH, AR, AR, AH, AR, AR, AR, _, _, _, _, _],
            [_, ST, AR, AH, AR, AR, AR, AR, AH, AR, AR, _, _, _, _, _],
            [AX, ST, AR, AR, AR, AR, AR, AR, AR, AR, AR, _, _, _, _, _],
            [_, _, AR, AR, AR, AR, AR, AR, AR, AR, AR, _, _, _, _, _],
            [_, _, AR, AR, _, _, _, _, AR, AR, _, _, _, _, _, _],
            [_, _, BT, BT, _, _, _, _, BT, BT, _, _, _, _, _, _],
            [_, BT, BT, _, _, _, _, _, _, BT, BT, _, _, _, _, _]
        ];
    },

    getGoblinSpriteRows() {
        const _ = null;
        const GR = '#4aaa30';
        const DG = '#2a6618';
        const YE = '#ffee00';
        const BK = '#0a0800';
        const TB = '#fffacc';
        const IR = '#5a5a7a';
        const LI = '#9090b8';
        const RU = '#252535';
        const BR = '#8c4a20';
        const LG = '#3a1e0e';
        const BT = '#181010';
        const SW = '#d4d4e8';
        const SH = '#707080';
        return [
            [_, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _],
            [_, _, _, _, GR, GR, GR, GR, GR, _, _, _, _, _, _, _],
            [_, _, DG, GR, GR, GR, GR, GR, GR, GR, DG, _, _, _, _, _],
            [_, _, _, GR, YE, GR, GR, GR, YE, GR, _, _, _, _, _, _],
            [_, _, _, GR, GR, GR, DG, GR, GR, GR, _, _, _, _, _, _],
            [_, _, _, GR, TB, BK, BK, BK, TB, GR, _, _, _, _, _, _],
            [_, _, _, IR, IR, IR, IR, IR, IR, IR, _, _, _, _, _, _],
            [SW, SH, IR, LI, IR, IR, RU, IR, IR, LI, IR, _, _, _, _, _],
            [_, SW, IR, IR, IR, BR, BR, IR, IR, IR, _, _, _, _, _, _],
            [_, SW, GR, IR, IR, IR, IR, IR, IR, GR, _, _, _, _, _, _],
            [_, _, GR, _, LG, LG, LG, LG, _, GR, _, _, _, _, _, _],
            [_, _, GR, _, LG, LG, LG, LG, _, GR, _, _, _, _, _, _],
            [_, _, _, _, LG, LG, _, LG, LG, _, _, _, _, _, _, _],
            [_, _, _, _, LG, LG, _, LG, LG, _, _, _, _, _, _, _],
            [_, _, _, _, BT, BT, _, BT, BT, _, _, _, _, _, _, _],
            [_, _, _, BT, BT, _, _, _, BT, BT, _, _, _, _, _, _]
        ];
    },

    getGoblinBruteSpriteRows() {
        const _ = null;
        const OG = '#7d8c2f';
        const DG = '#495518';
        const EY = '#140800';
        const SK = '#b58d5f';
        const MK = '#23120a';
        const TU = '#efe1b4';
        const PL = '#7d4730';
        const PH = '#b36a48';
        const BD = '#4f2c1c';
        const AX = '#d4d3d0';
        const SH = '#74726b';
        const BT = '#1e140f';
        return [
            [_, _, _, _, _, _, DG, DG, DG, _, _, _, _, _, _, _],
            [_, _, _, _, DG, OG, OG, OG, OG, DG, _, _, _, _, _, _],
            [_, _, _, DG, OG, OG, OG, OG, OG, OG, DG, _, _, _, _, _],
            [_, _, _, OG, SK, OG, OG, OG, OG, SK, OG, _, _, _, _, _],
            [_, _, _, OG, OG, MK, OG, OG, MK, OG, OG, _, _, _, _, _],
            [_, _, _, OG, TU, MK, MK, MK, TU, OG, OG, _, _, _, _, _],
            [AX, SH, BD, PL, PL, PL, PL, PL, PL, PL, BD, _, _, _, _, _],
            [_, AX, BD, PH, PL, PL, PH, PL, PL, PH, BD, _, _, _, _, _],
            [_, _, BD, PL, PL, PL, PL, PL, PL, PL, BD, _, _, _, _, _],
            [_, _, OG, BD, PL, PL, PL, PL, PL, BD, OG, _, _, _, _, _],
            [_, _, OG, _, BD, BD, BD, BD, BD, _, OG, _, _, _, _, _],
            [_, _, OG, _, BD, BD, BD, BD, BD, _, OG, _, _, _, _, _],
            [_, _, _, _, BD, BD, _, _, BD, BD, _, _, _, _, _, _],
            [_, _, _, _, BD, BD, _, _, BD, BD, _, _, _, _, _, _],
            [_, _, _, _, BT, BT, _, _, BT, BT, _, _, _, _, _, _],
            [_, _, _, BT, BT, _, _, _, _, BT, BT, _, _, _, _, _]
        ];
    },

    getGoblinArcherSpriteRows() {
        const _ = null;
        const GR = '#58b93a';
        const DG = '#2f6b1e';
        const EY = '#120a02';
        const SK = '#cf9b6f';
        const TU = '#4d6f3a';
        const CL = '#3b4f2d';
        const BW = '#8b5d34';
        const BD = '#5f3c22';
        const ST = '#1f1710';
        return [
            [_, _, _, _, _, _, DG, DG, DG, _, _, _, _, _, _, _],
            [_, _, _, _, DG, GR, GR, GR, GR, DG, _, _, _, _, _, _],
            [_, _, _, DG, GR, GR, GR, GR, GR, GR, DG, _, _, _, _, _],
            [_, _, _, GR, SK, GR, GR, GR, GR, SK, GR, _, _, _, _, _],
            [_, _, _, GR, GR, EY, GR, GR, EY, GR, GR, _, _, _, _, _],
            [_, _, _, _, SK, SK, SK, SK, SK, SK, _, _, _, _, _, _],
            [_, _, _, TU, TU, TU, TU, TU, TU, TU, TU, _, _, _, _, _],
            [_, _, CL, CL, TU, TU, TU, TU, TU, TU, CL, CL, _, _, _, _],
            [_, _, CL, TU, TU, TU, TU, TU, TU, TU, TU, CL, _, _, _, _],
            [_, _, _, CL, TU, TU, TU, TU, TU, TU, CL, _, _, _, _, _],
            [_, _, _, CL, CL, TU, TU, TU, TU, CL, CL, _, _, _, _, _],
            [_, _, _, _, ST, CL, CL, CL, CL, ST, _, _, _, _, _, _],
            [_, _, _, _, ST, ST, _, _, ST, ST, _, _, _, _, _, _],
            [_, _, _, _, ST, ST, _, _, ST, ST, _, _, _, _, _, _],
            [_, _, _, BW, BD, _, _, _, _, BD, BW, _, _, _, _, _],
            [_, _, BW, BD, _, _, _, _, _, _, BD, BW, _, _, _, _]
        ];
    },

    getGoblinShamanSpriteRows() {
        const _ = null;
        const GR = '#5aa13b';
        const DG = '#2b5d1c';
        const EY = '#120a02';
        const SK = '#d2a079';
        const RO = '#6b4aa7';
        const RH = '#a07bdb';
        const ST = '#241814';
        const BO = '#8a623c';
        const WO = '#e7d7a8';
        return [
            [_, _, _, _, _, _, DG, DG, DG, _, _, _, _, _, _, _],
            [_, _, _, _, DG, GR, GR, GR, GR, DG, _, _, _, _, _, _],
            [_, _, _, DG, GR, GR, GR, GR, GR, GR, DG, _, _, _, _, _],
            [_, _, _, GR, SK, GR, GR, GR, GR, SK, GR, _, _, _, _, _],
            [_, _, _, GR, GR, EY, GR, GR, EY, GR, GR, _, _, _, _, _],
            [_, _, _, _, SK, SK, SK, SK, SK, SK, _, _, _, _, _, _],
            [_, _, _, RO, RO, RO, RH, RH, RO, RO, RO, _, _, _, _, _],
            [_, _, RO, RH, RH, RO, RO, RO, RO, RH, RH, RO, _, _, _, _],
            [_, _, RO, RH, RH, RH, RO, RH, RH, RH, RH, RO, _, _, _, _],
            [_, _, _, RO, RH, RH, RH, RH, RH, RH, RO, _, _, _, _, _],
            [_, _, _, RO, RH, RH, WO, WO, RH, RH, RO, _, _, _, _, _],
            [_, _, _, _, ST, RO, RH, RH, RO, ST, _, _, _, _, _, _],
            [_, _, _, _, ST, ST, _, _, ST, ST, _, _, _, _, _, _],
            [_, _, _, _, ST, ST, _, _, ST, ST, _, _, _, _, _, _],
            [_, _, _, BO, BO, _, _, _, _, BO, BO, _, _, _, _, _],
            [_, _, BO, BO, _, _, _, _, _, _, BO, BO, _, _, _, _]
        ];
    },

    getClericSpriteRows() {
        const _ = null;
        const WI = '#f0ece4';
        const WS = '#c4c0b8';
        const SK = '#e0a878';
        const EY = '#1c1008';
        const LI = '#c07060';
        const RB = '#eaeae2';
        const RS = '#b0aca4';
        const GD = '#d4a820';
        const BT = '#2c1c14';
        const MH = '#7a5030';
        const MB = '#909098';
        return [
            [_, _, _, _, _, WI, WI, WI, WI, WI, _, _, _, _, _, _],
            [_, _, _, _, WS, WI, WI, WI, WI, WS, _, _, _, _, _, _],
            [_, _, _, WS, WI, SK, SK, SK, SK, WI, WS, _, _, _, _, _],
            [_, _, _, WS, SK, SK, SK, SK, SK, SK, WS, _, _, _, _, _],
            [_, _, _, WS, SK, EY, SK, EY, SK, SK, WS, _, _, _, _, _],
            [_, _, _, _, SK, SK, LI, SK, SK, SK, _, _, _, _, _, _],
            [_, _, _, _, GD, GD, GD, GD, GD, GD, _, _, _, _, _, _],
            [MH, _, RB, RB, RB, RB, RB, RB, RB, RB, _, _, _, _, _, _],
            [MB, MH, RS, RB, GD, RB, RB, RB, RS, RB, _, _, _, _, _, _],
            [_, MH, RB, GD, GD, GD, RB, RB, RB, RB, _, _, _, _, _, _],
            [_, _, RB, RB, GD, RB, RB, RB, RB, RB, _, _, _, _, _, _],
            [_, _, RB, RB, RB, RB, RB, RB, RB, RB, _, _, _, _, _, _],
            [_, _, RS, RB, RB, RB, RB, RB, RB, RS, _, _, _, _, _, _],
            [_, _, RS, RB, RB, _, _, RB, RB, RS, _, _, _, _, _, _],
            [_, _, _, BT, BT, _, _, BT, BT, _, _, _, _, _, _, _],
            [_, _, BT, BT, _, _, _, _, BT, BT, _, _, _, _, _, _]
        ];
    },

    getRangerSpriteRows() {
        const _ = null;
        const HD = '#2f4f2f';
        const HL = '#4f7a45';
        const SK = '#e0ba8a';
        const EY = '#1a1008';
        const TU = '#6f8f5a';
        const CL = '#3d5c3a';
        const SH = '#2a3c29';
        const BW = '#8a5f3a';
        const BD = '#5b3a22';
        const ST = '#403020';
        return [
            [_, _, _, _, _, _, HD, HD, HD, _, _, _, _, _, _, _],
            [_, _, _, _, _, HD, HL, HL, HL, HD, _, _, _, _, _, _],
            [_, _, _, _, HD, HL, HL, HL, HL, HL, HD, _, _, _, _, _],
            [_, _, _, HD, HL, SK, SK, SK, SK, HL, HD, BW, _, _, _, _],
            [_, _, _, HD, SK, EY, SK, EY, SK, SK, HD, BD, _, _, _, _],
            [_, _, _, _, SK, SK, SK, SK, SK, SK, _, BW, _, _, _, _],
            [_, _, _, TU, TU, TU, TU, TU, TU, TU, TU, BD, _, _, _, _],
            [_, _, SH, CL, TU, TU, TU, TU, TU, TU, CL, SH, BW, _, _, _],
            [_, _, SH, CL, TU, TU, TU, TU, TU, TU, CL, SH, BD, _, _, _],
            [_, _, _, SH, CL, TU, TU, TU, TU, CL, SH, BW, _, _, _, _],
            [_, _, _, SH, CL, CL, TU, TU, CL, SH, _, _, _, _, _, _],
            [_, _, _, _, ST, CL, CL, CL, CL, ST, _, _, _, _, _, _],
            [_, _, _, _, ST, ST, _, _, ST, ST, _, _, _, _, _, _],
            [_, _, _, _, ST, ST, _, _, ST, ST, _, _, _, _, _, _],
            [_, _, _, BW, BD, _, _, _, _, BD, BW, _, _, _, _, _],
            [_, _, BW, BD, _, _, _, _, _, _, BD, BW, _, _, _, _]
        ];
    }
};
