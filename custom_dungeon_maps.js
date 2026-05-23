// Authored dungeon layouts with layered base, prop, and encounter grids.
(() => {
    const createTokenRow = (row) => row.trim().split(/\s+/);

    window.CustomDungeonMaps = {
        createTokenRow,

        starterKeep: {
            id: 'starter-keep',
            name: 'Starter Keep',
            size: 21,
            baseRows: [
                createTokenRow('wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa'),
                createTokenRow('wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ __ __ wa'),
                createTokenRow('wa wa wa __ wa wa wa wa wa __ wa wa wa wa wa __ wa wa wa wa wa'),
                createTokenRow('wa __ __ __ __ __ __ __ wa __ wa __ __ __ wa __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ wa __ wa __ __ __ wa __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ wa __ __ __ __ __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ wa __ wa __ __ __ wa __ __ __ __ __ wa'),
                createTokenRow('wa wa wa __ wa wa wa wa wa __ wa __ wa wa wa wa wa __ wa wa wa'),
                createTokenRow('wa __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ wa'),
                createTokenRow('wa __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ wa'),
                createTokenRow('wa __ __ wa wa __ __ wa wa wa wa __ wa wa wa wa wa __ __ wa wa'),
                createTokenRow('wa __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ wa'),
                createTokenRow('wa __ __ __ wa __ __ __ __ __ wa __ __ __ __ __ wa __ __ __ wa'),
                createTokenRow('wa wa __ __ wa __ __ wa wa __ __ __ wa wa __ __ wa __ __ wa wa'),
                createTokenRow('wa __ __ __ __ __ __ __ wa __ __ __ wa __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ wa __ __ __ wa __ __ __ __ __ __ __ wa'),
                createTokenRow('wa __ __ __ __ __ __ __ wa __ __ __ wa __ __ __ __ __ __ __ wa'),
                createTokenRow('wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa wa')
            ],
            propRows: [
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ bd __ __ __ __ wr1 __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ chI __ __ chS __ __ __ __ __ __ __ __ __ __ __ __ wr2 __ chG __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ tb __ chI __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ st1 __ __ __ st2 __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ br1 __ cr __ __ __ __ __ __ __ __ __ __ __ __ __ br2 __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ chG __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ chS __'),
                createTokenRow('__ bd __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __')
            ],
            encounterRows: [
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ GCB GB GW GA __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ GW GS GA __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ GW __ GS __ __ __ __ __ __ __ __ __ __ __ __ __ GA __ __ __'),
                createTokenRow('__ __ __ __ GSP __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ GW __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ GB __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ GA __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ GB __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ GSP __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ PW __ PR __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ PZ __ PC __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __'),
                createTokenRow('__ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __ __')
            ],
            baseLegend: {
                wa: { type: 'wall' },
                dr: { type: 'door' },
                __: { type: 'floor' }
            },
            propLegend: {
                bd: { frameId: 'bed', roomTheme: 'barracks', searchable: false },
                tb: { frameId: 'tableCandles', roomTheme: 'barracks', searchable: false },
                wr1: { frameId: 'weaponRack1', roomTheme: 'armory', searchable: true },
                wr2: { frameId: 'weaponRack2', roomTheme: 'armory', searchable: true },
                chI: { frameId: 'chestClosedIron', roomTheme: 'treasure', searchable: true },
                chS: { frameId: 'chestClosedSteel', roomTheme: 'treasure', searchable: true },
                chG: {
                    frameId: 'chestClosedGold',
                    roomTheme: 'treasure',
                    searchable: true,
                    lootMode: 'all',
                    goldAmount: 50,
                    lootItemIds: ['healers-circlet', 'mages-amulet', 'long-bow', 'small-shield']
                },
                br1: { frameId: 'barrels1', roomTheme: 'storage', searchable: true },
                br2: { frameId: 'barrels2', roomTheme: 'storage', searchable: true },
                cr: { frameId: 'crate', roomTheme: 'storage', searchable: true },
                st1: { frameId: 'spikeTrap1', roomTheme: 'trap', searchable: false },
                st2: { frameId: 'spikeTrap2', roomTheme: 'trap', searchable: false }
            },
            encounterLegend: {
                PW: { kind: 'player', characterId: 'warrior' },
                PZ: { kind: 'player', characterId: 'wizard' },
                PC: { kind: 'player', characterId: 'cleric' },
                PR: { kind: 'player', characterId: 'ranger' },
                GW: {
                    kind: 'enemy',
                    archetypeId: 'goblin-warrior',
                    lootMode: 'all',
                    lootItemIds: ['small-shield']
                },
                GA: { kind: 'enemy', archetypeId: 'goblin-archer' },
                GS: { kind: 'enemy', archetypeId: 'goblin-shaman' },
                GB: { kind: 'enemy', archetypeId: 'goblin-brute' },
                GSP: {
                    kind: 'enemy',
                    archetypeId: 'giant-spider',
                    experiencePoints: 2000
                },
                GCB: { kind: 'enemy', archetypeId: 'goblin-chieftain' },
            }
        }
    };
})();