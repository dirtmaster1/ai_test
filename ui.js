// UI - HUD, combat cards, ability icons, victory/game over screens
window.GridUI = {

    getAbilityIconSvg(ability) {
        if (ability.id === 'bow-shot') {
            return `
                <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
                    <path d="M7 4c6 2.5 6 13.5 0 16" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/>
                    <path d="M7 12h10" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/>
                    <path d="M16.8 9.8L21 12l-4.2 2.2" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>
                </svg>`;
        }

        if (ability.id === 'dagger-strike') {
            return `
                <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
                    <path d="M14.8 3.8l5.4 5.4-1.8 1.8-5.4-5.4z" fill="currentColor" opacity="0.9"/>
                    <path d="M5 19l7.5-7.5-2-2L3 17z" fill="currentColor"/>
                    <path d="M3.2 20.8l1.8-1.8 1 1-1.8 1.8z" fill="currentColor" opacity="0.65"/>
                </svg>`;
        }

        if (ability.id === 'magic-missile') {
            return `
                <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
                    <path d="M5 12c2.4-4.8 6.2-7.2 10.8-7.2l-1.8 2.7 4-.4-.9-3.9-1.5 2.2C9.8 5.6 5.6 8.6 3.4 14.1" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>
                    <circle cx="17.2" cy="15.6" r="2.3" fill="currentColor" opacity="0.95"/>
                    <circle cx="19.8" cy="11.3" r="1.1" fill="currentColor" opacity="0.55"/>
                </svg>`;
        }

        if (ability.id === 'heal') {
            return `
                <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
                    <rect x="10" y="3" width="4" height="18" rx="1.5" fill="currentColor"/>
                    <rect x="3" y="10" width="18" height="4" rx="1.5" fill="currentColor"/>
                </svg>`;
        }

        if (ability.id === 'mace-strike') {
            return `
                <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
                    <circle cx="15.5" cy="8.5" r="4" fill="currentColor" opacity="0.88"/>
                    <path d="M12.1 11.9L5 19" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"/>
                </svg>`;
        }

        if (ability.id === 'battle-shout') {
            return `
                <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
                    <path d="M3 9l4 3-4 3V9z" fill="currentColor"/>
                    <path d="M7 11.5h6" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
                    <path d="M7 8.5h10" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" opacity="0.7"/>
                    <path d="M7 14.5h10" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" opacity="0.7"/>
                </svg>`;
        }

        if (ability.id === 'inflict-pain') {
            return `
                <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
                    <path d="M12 3l1.8 4.7L19 9.2l-4 3.3 1.3 5.3L12 15.2 7.7 17.8 9 12.5 5 9.2l5.2-1.5z" fill="currentColor" opacity="0.9"/>
                    <path d="M12 6.5v11" stroke="#140b1e" stroke-width="1.6" stroke-linecap="round" opacity="0.65"/>
                </svg>`;
        }

        return `
            <svg viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
                <path d="M16.8 3.6l3.6 3.6-2.3 2.3-3.6-3.6z" fill="currentColor" opacity="0.92"/>
                <path d="M8.1 15.9L15.2 8.8l-2-2-7.1 7.1-.9 4z" fill="currentColor"/>
                <path d="M5.8 17.2l1-1 1 1-1 1z" fill="#120f0b"/>
                <path d="M4.2 19.8l2.6-2.6 1.6 1.6-2.6 2.6z" fill="currentColor" opacity="0.7"/>
                <path d="M14.2 7.8l2-2" fill="none" stroke="#120f0b" stroke-width="1.2" stroke-linecap="round"/>
            </svg>`;
    },

    getAbilityDetailText(character, ability) {
        if (!ability) {
            return 'Effect';
        }

        if (ability.type === 'heal') {
            const healAmount = ability.healAmount ?? 0;
            return `Healing: ${healAmount} HP`;
        }

        if (ability.type === 'buff') {
            if (ability.id === 'battle-shout') {
                const acBonus = ability.acBonus ?? 1;
                return `Effect: +${acBonus} AC`;
            }
            if (ability.id === 'inflict-pain') {
                const damageBonus = ability.damageBonus ?? 1;
                return `Effect: +${damageBonus} DMG`;
            }
            return 'Effect';
        }

        if (ability.type === 'attack') {
            const damageAmount = ability.damage ?? character.meleeAttackDamage;
            const attackKind = ability.id === 'magic-missile'
                ? 'Magic'
                : ability.id === 'bow-shot'
                    ? 'Ranged'
                    : 'Melee';
            return `${attackKind}: ${damageAmount} DMG`;
        }

        return 'Effect';
    },

    getAbilityTooltipText(character, ability) {
        const lines = [ability.name, this.getAbilityDetailText(character, ability)];
        if (typeof ability.range === 'number') {
            lines.push(`Range: ${ability.range}`);
        }
        if ((ability.mpCost ?? 0) > 0) {
            lines.push(`Cost: ${ability.mpCost} MP`);
        }
        return lines.join('\n');
    },

    getAbilityDescriptionText(ability) {
        if (!ability) {
            return 'No description.';
        }

        if (ability.type === 'heal') {
            return 'Restore hit points to an ally within range.';
        }

        if (ability.type === 'buff') {
            if (ability.id === 'battle-shout') {
                return 'Bolster nearby allies with extra armor.';
            }
            if (ability.id === 'inflict-pain') {
                return 'Empower nearby allies with extra damage.';
            }
            return 'Apply a supportive combat effect.';
        }

        if (ability.type === 'attack') {
            if ((ability.range ?? 1) > 1) {
                return 'Deal damage from a safer distance.';
            }
            return 'Deal close-range physical damage.';
        }

        return 'Use this technique in combat.';
    },

    getCombatLogTimestamp() {
        const now = new Date();
        return now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' });
    },

    getCharacterAttackSourceLabel(character, ability = null) {
        const handsItem = character?.equipment?.hands;
        if (handsItem) {
            return handsItem.replace(/\s*\([^)]*\)\s*$/, '').trim();
        }

        if (ability?.name) {
            return ability.name;
        }

        return 'attack';
    },

    closeCombatLogWindow() {
        if (!this.combatLogWindow?.root) {
            return;
        }

        this.combatLogWindow.root.style.display = 'none';
        if (this.combatLogWindow.reopenButton) {
            this.combatLogWindow.reopenButton.style.display = 'inline-flex';
        }
    },

    openCombatLogWindow() {
        if (!this.combatLogWindow?.root) {
            this.setupCombatLogWindow();
        }

        this.combatLogWindow.root.style.display = 'flex';
        if (this.combatLogWindow.reopenButton) {
            this.combatLogWindow.reopenButton.style.display = 'none';
        }
    },

    setupCombatLogWindow() {
        if (this.combatLogWindow?.root) {
            return;
        }

        const root = document.createElement('div');
        root.id = 'combatLogWindow';
        root.style.position = 'fixed';
        root.style.right = '20px';
        root.style.bottom = '20px';
        root.style.width = 'min(420px, calc(100vw - 32px))';
        root.style.height = '220px';
        root.style.minWidth = '280px';
        root.style.minHeight = '160px';
        root.style.maxWidth = 'calc(100vw - 16px)';
        root.style.maxHeight = 'calc(100vh - 16px)';
        root.style.display = 'none';
        root.style.flexDirection = 'column';
        root.style.border = '1px solid rgba(232, 224, 202, 0.22)';
        root.style.borderRadius = '12px';
        root.style.background = 'linear-gradient(180deg, rgba(23, 24, 28, 0.96), rgba(10, 10, 12, 0.96))';
        root.style.boxShadow = '0 18px 44px rgba(0, 0, 0, 0.45), inset 0 0 0 1px rgba(255, 255, 255, 0.04)';
        root.style.backdropFilter = 'blur(8px)';
        root.style.overflow = 'hidden';
        root.style.resize = 'both';
        root.style.zIndex = '40';
        root.style.pointerEvents = 'auto';

        const header = document.createElement('div');
        header.style.display = 'flex';
        header.style.alignItems = 'center';
        header.style.justifyContent = 'space-between';
        header.style.gap = '12px';
        header.style.padding = '10px 12px';
        header.style.borderBottom = '1px solid rgba(255, 255, 255, 0.08)';
        header.style.background = 'linear-gradient(180deg, rgba(54, 58, 70, 0.7), rgba(24, 26, 32, 0.72))';
        header.style.cursor = 'move';
        header.style.userSelect = 'none';

        const titleWrap = document.createElement('div');
        titleWrap.style.minWidth = '0';

        const title = document.createElement('div');
        title.textContent = 'Combat Log';
        title.style.fontSize = '14px';
        title.style.fontWeight = '700';
        title.style.letterSpacing = '0.04em';
        title.style.color = '#f0e8d2';

        const subtitle = document.createElement('div');
        subtitle.textContent = 'Attacks, spells, heals, and buffs';
        subtitle.style.marginTop = '2px';
        subtitle.style.fontSize = '10px';
        subtitle.style.letterSpacing = '0.08em';
        subtitle.style.textTransform = 'uppercase';
        subtitle.style.color = '#a89c82';

        titleWrap.appendChild(title);
        titleWrap.appendChild(subtitle);

        const clearButton = document.createElement('button');
        clearButton.type = 'button';
        clearButton.textContent = 'Clear';
        clearButton.style.padding = '4px 8px';
        clearButton.style.border = '1px solid rgba(255,255,255,0.16)';
        clearButton.style.borderRadius = '999px';
        clearButton.style.background = 'rgba(255,255,255,0.06)';
        clearButton.style.color = '#d6cbb8';
        clearButton.style.fontSize = '10px';
        clearButton.style.cursor = 'pointer';
        clearButton.style.pointerEvents = 'auto';

        const closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.textContent = 'Close';
        closeButton.style.padding = '4px 8px';
        closeButton.style.border = '1px solid rgba(255,255,255,0.16)';
        closeButton.style.borderRadius = '999px';
        closeButton.style.background = 'rgba(255,255,255,0.06)';
        closeButton.style.color = '#d6cbb8';
        closeButton.style.fontSize = '10px';
        closeButton.style.cursor = 'pointer';
        closeButton.style.pointerEvents = 'auto';

        const actionButtons = document.createElement('div');
        actionButtons.style.display = 'flex';
        actionButtons.style.gap = '6px';
        actionButtons.appendChild(clearButton);
        actionButtons.appendChild(closeButton);

        header.appendChild(titleWrap);
        header.appendChild(actionButtons);

        const body = document.createElement('div');
        body.style.flex = '1 1 auto';
        body.style.padding = '10px 12px';
        body.style.overflowY = 'auto';
        body.style.overflowX = 'hidden';
        body.style.display = 'flex';
        body.style.flexDirection = 'column';
        body.style.gap = '8px';
        body.style.scrollBehavior = 'smooth';

        const emptyState = document.createElement('div');
        emptyState.textContent = 'Combat actions will appear here.';
        emptyState.style.padding = '10px 12px';
        emptyState.style.border = '1px dashed rgba(255, 255, 255, 0.12)';
        emptyState.style.borderRadius = '10px';
        emptyState.style.color = '#8f8470';
        emptyState.style.fontSize = '12px';
        emptyState.style.lineHeight = '1.5';
        body.appendChild(emptyState);

        root.appendChild(header);
        root.appendChild(body);
        document.body.appendChild(root);

        const reopenButton = document.createElement('button');
        reopenButton.type = 'button';
        reopenButton.textContent = 'Combat Log';
        reopenButton.style.position = 'fixed';
        reopenButton.style.right = '20px';
        reopenButton.style.bottom = '20px';
        reopenButton.style.padding = '8px 12px';
        reopenButton.style.borderRadius = '999px';
        reopenButton.style.border = '1px solid rgba(232, 224, 202, 0.22)';
        reopenButton.style.background = 'linear-gradient(180deg, rgba(32, 35, 42, 0.96), rgba(12, 12, 14, 0.96))';
        reopenButton.style.color = '#efe5d0';
        reopenButton.style.fontSize = '11px';
        reopenButton.style.letterSpacing = '0.05em';
        reopenButton.style.cursor = 'pointer';
        reopenButton.style.boxShadow = '0 10px 24px rgba(0, 0, 0, 0.35)';
        reopenButton.style.zIndex = '41';
        reopenButton.style.display = 'inline-flex';
        reopenButton.style.pointerEvents = 'auto';
        document.body.appendChild(reopenButton);

        const startDrag = (event) => {
            if (event.target === clearButton) {
                return;
            }

            const rect = root.getBoundingClientRect();
            const offsetX = event.clientX - rect.left;
            const offsetY = event.clientY - rect.top;
            root.style.left = `${rect.left}px`;
            root.style.top = `${rect.top}px`;
            root.style.right = 'auto';
            root.style.bottom = 'auto';

            const onMove = (moveEvent) => {
                const maxLeft = Math.max(8, window.innerWidth - root.offsetWidth - 8);
                const maxTop = Math.max(8, window.innerHeight - root.offsetHeight - 8);
                const nextLeft = Math.min(Math.max(8, moveEvent.clientX - offsetX), maxLeft);
                const nextTop = Math.min(Math.max(8, moveEvent.clientY - offsetY), maxTop);
                root.style.left = `${nextLeft}px`;
                root.style.top = `${nextTop}px`;
            };

            const onUp = () => {
                window.removeEventListener('pointermove', onMove);
                window.removeEventListener('pointerup', onUp);
            };

            window.addEventListener('pointermove', onMove);
            window.addEventListener('pointerup', onUp);
        };

        header.addEventListener('pointerdown', startDrag);
        clearButton.addEventListener('click', () => {
            body.innerHTML = '';
            body.appendChild(emptyState);
            this.combatLogEntries = [];
        });
        closeButton.addEventListener('click', () => this.closeCombatLogWindow());
        reopenButton.addEventListener('click', () => this.openCombatLogWindow());

        this.combatLogEntries = [];
        this.combatLogWindow = { root, body, emptyState, reopenButton };
    },

    appendCombatLogEntry(message, accentColor = '#d6cbb8') {
        if (!message) {
            return;
        }

        if (!this.combatLogWindow?.body) {
            this.setupCombatLogWindow();
        }

        const { body, emptyState } = this.combatLogWindow;
        if (emptyState.parentNode === body) {
            body.removeChild(emptyState);
        }

        const row = document.createElement('div');
        row.style.padding = '8px 10px';
        row.style.border = '1px solid rgba(255,255,255,0.08)';
        row.style.borderLeft = `3px solid ${accentColor}`;
        row.style.borderRadius = '10px';
        row.style.background = 'rgba(255,255,255,0.04)';
        row.style.boxShadow = 'inset 0 0 0 1px rgba(255,255,255,0.02)';

        const timestamp = document.createElement('div');
        timestamp.textContent = this.getCombatLogTimestamp();
        timestamp.style.fontSize = '10px';
        timestamp.style.letterSpacing = '0.08em';
        timestamp.style.textTransform = 'uppercase';
        timestamp.style.color = '#8f8470';
        timestamp.style.marginBottom = '4px';

        const text = document.createElement('div');
        text.textContent = message;
        text.style.fontSize = '12px';
        text.style.lineHeight = '1.5';
        text.style.color = '#efe5d0';

        row.appendChild(timestamp);
        row.appendChild(text);
        body.appendChild(row);

        this.combatLogEntries.push(message);
        if (this.combatLogEntries.length > 80) {
            this.combatLogEntries.shift();
            if (body.firstChild) {
                body.removeChild(body.firstChild);
            }
        }

        body.scrollTop = body.scrollHeight;
    },

    setupUI() {
        const hudRoot = document.getElementById('battleHud') || this.container;
        hudRoot.style.pointerEvents = 'none';
        hudRoot.innerHTML = '';
        this.activeInventoryCharacter = null;
        this.activeInventoryTab = 'info';
        this.turnQueueState = '';

        const turnQueueSection = this.createTurnOrderQueuePanel();
        hudRoot.appendChild(turnQueueSection.section);

        this.characters.forEach((character) => {
            const card = this.createCombatCard(character);
            turnQueueSection.list.appendChild(card.card);
            this.characterHud.set(character.id, card);
        });

        this.victoryText = document.createElement('div');
        this.victoryText.id = 'victoryText';
        this.victoryText.textContent = 'Victory!';
        this.victoryText.style.position = 'absolute';
        this.victoryText.style.top = '50%';
        this.victoryText.style.left = '50%';
        this.victoryText.style.transform = 'translate(-50%, -50%)';
        this.victoryText.style.fontFamily = 'Arial, sans-serif';
        this.victoryText.style.fontSize = '96px';
        this.victoryText.style.fontWeight = '900';
        this.victoryText.style.letterSpacing = '4px';
        this.victoryText.style.color = '#ffd700';
        this.victoryText.style.textShadow = '0 0 16px rgba(255, 215, 0, 0.6), 0 0 32px rgba(255, 140, 0, 0.45)';
        this.victoryText.style.pointerEvents = 'none';
        this.victoryText.style.opacity = '0';
        this.victoryText.style.display = 'none';
        this.victoryText.style.zIndex = '20';

        this.container.style.position = 'relative';
        this.container.appendChild(this.victoryText);
        this.setupTargetPreviewPanel();
        this.setupCharacterInventoryModal();
        this.setupLootMenuWindow();
        this.setupCombatLogWindow();
        this.updateTurnOrderQueue(this.getActiveTurnCharacter());
    },

    createTurnOrderQueuePanel() {
        const section = document.createElement('section');
        section.style.marginBottom = '0';
        section.style.padding = '8px 8px 6px';
        section.style.border = '1px solid rgba(232, 224, 202, 0.18)';
        section.style.borderRadius = '8px';
        section.style.background = 'linear-gradient(180deg, rgba(21, 20, 18, 0.95), rgba(10, 10, 10, 0.95))';
        section.style.pointerEvents = 'auto';

        const heading = document.createElement('div');
        heading.style.marginBottom = '6px';

        const title = document.createElement('div');
        title.style.fontSize = '11px';
        title.style.fontWeight = '700';
        title.style.letterSpacing = '0.08em';
        title.style.textTransform = 'uppercase';
        title.style.color = '#e3d6bb';
        title.textContent = 'Party Status';

        const subtitle = document.createElement('div');
        subtitle.style.marginTop = '2px';
        subtitle.style.fontSize = '9px';
        subtitle.style.color = '#8d8169';
        subtitle.textContent = 'Exploration Mode';

        heading.appendChild(title);
        heading.appendChild(subtitle);
        section.appendChild(heading);

        const list = document.createElement('div');
        list.style.display = 'grid';
        list.style.gap = '6px';
        list.style.overflowX = 'hidden';
        list.style.overflowY = 'auto';
        list.style.maxHeight = 'calc(100vh - 120px)';
        list.style.scrollbarWidth = 'thin';
        section.appendChild(list);

        this.turnOrderQueuePanel = { section, list, title, subtitle };
        return this.turnOrderQueuePanel;
    },

    updateTurnOrderQueue(activeCharacter) {
        if (!this.turnOrderQueuePanel?.list) {
            return;
        }

        const aliveTurnOrder = this.getAliveTurnOrder();
        const sequenceIds = [];
        if (aliveTurnOrder.length > 0) {
            const activeIndex = Math.max(0, aliveTurnOrder.indexOf(activeCharacter));
            for (let offset = 0; offset < aliveTurnOrder.length; offset += 1) {
                const queueCharacter = aliveTurnOrder[(activeIndex + offset) % aliveTurnOrder.length];
                sequenceIds.push(queueCharacter.id);
            }
        }

        const deadIds = this.characters
            .filter((character) => character.isDead)
            .map((character) => character.id);
        const orderedIds = [...sequenceIds, ...deadIds];

        const nextState = `${activeCharacter?.id ?? 'none'}|${orderedIds.join('|')}`;
        if (this.turnQueueState !== nextState) {
            this.turnQueueState = nextState;
            const list = this.turnOrderQueuePanel.list;

            // Rebuild the roster list from scratch so cards not in the current turn roster are removed.
            while (list.firstChild) {
                list.removeChild(list.firstChild);
            }

            orderedIds.forEach((characterId) => {
                const hud = this.characterHud.get(characterId);
                if (hud?.card) {
                    list.appendChild(hud.card);
                }
            });
        }

        if (this.turnOrderQueuePanel.title) {
            this.turnOrderQueuePanel.title.textContent = this.gameMode === 'combat'
                ? 'Combat Mode'
                : 'Exploration Mode';
        }

        if (this.turnOrderQueuePanel.subtitle) {
            this.turnOrderQueuePanel.subtitle.textContent = this.gameMode === 'combat'
                ? 'Initiative order with party and enemy status'
                : 'Party movement and marching order';
        }

        this.characters.forEach((character) => {
            const hud = this.characterHud.get(character.id);
            if (!hud?.turnBadge) {
                return;
            }

            if (character.isDead) {
                hud.turnBadge.textContent = 'Out';
                hud.turnBadge.style.borderColor = 'rgba(140, 140, 140, 0.45)';
                hud.turnBadge.style.background = 'rgba(120, 120, 120, 0.12)';
                hud.turnBadge.style.color = '#9b9b9b';
                return;
            }

            const queueIndex = sequenceIds.indexOf(character.id);
            if (queueIndex === 0) {
                hud.turnBadge.textContent = 'Now';
                hud.turnBadge.style.borderColor = this.hexToRgba(character.accentColor, 0.78);
                hud.turnBadge.style.background = this.hexToRgba(character.accentColor, 0.20);
                hud.turnBadge.style.color = '#f0e8d2';
                return;
            }

            if (queueIndex > 0) {
                hud.turnBadge.textContent = `+${queueIndex}`;
                hud.turnBadge.style.borderColor = 'rgba(255,255,255,0.18)';
                hud.turnBadge.style.background = 'rgba(255,255,255,0.04)';
                hud.turnBadge.style.color = '#bcb29c';
                return;
            }

            hud.turnBadge.textContent = 'Waiting';
            hud.turnBadge.style.borderColor = 'rgba(255,255,255,0.18)';
            hud.turnBadge.style.background = 'rgba(255,255,255,0.04)';
            hud.turnBadge.style.color = '#bcb29c';
        });
    },

    setupTargetPreviewPanel() {
        const panel = document.createElement('div');
        panel.style.position = 'absolute';
        panel.style.left = '0px';
        panel.style.top = '0px';
        panel.style.transform = 'none';
        panel.style.display = 'none';
        panel.style.minWidth = '220px';
        panel.style.maxWidth = 'min(320px, calc(100% - 24px))';
        panel.style.padding = '10px 12px';
        panel.style.border = '1px solid rgba(232, 224, 202, 0.20)';
        panel.style.borderRadius = '10px';
        panel.style.background = 'linear-gradient(180deg, rgba(24, 25, 31, 0.96), rgba(10, 10, 14, 0.94))';
        panel.style.boxShadow = '0 14px 32px rgba(0, 0, 0, 0.38)';
        panel.style.backdropFilter = 'blur(8px)';
        panel.style.pointerEvents = 'none';
        panel.style.zIndex = '26';

        const title = document.createElement('div');
        title.style.fontSize = '12px';
        title.style.fontWeight = '700';
        title.style.letterSpacing = '0.04em';
        title.style.color = '#f0e8d2';

        const subtitle = document.createElement('div');
        subtitle.style.marginTop = '3px';
        subtitle.style.fontSize = '10px';
        subtitle.style.letterSpacing = '0.08em';
        subtitle.style.textTransform = 'uppercase';
        subtitle.style.color = '#9da5b7';

        const effect = document.createElement('div');
        effect.style.marginTop = '8px';
        effect.style.fontSize = '13px';
        effect.style.fontWeight = '700';
        effect.style.color = '#f0e8d2';

        const detail = document.createElement('div');
        detail.style.marginTop = '4px';
        detail.style.fontSize = '11px';
        detail.style.lineHeight = '1.45';
        detail.style.color = '#bcb29c';

        panel.appendChild(title);
        panel.appendChild(subtitle);
        panel.appendChild(effect);
        panel.appendChild(detail);
        this.container.appendChild(panel);

        this.targetPreviewPanel = { panel, title, subtitle, effect, detail };
    },

    positionTargetPreviewPanel(hoveredCharacter) {
        const preview = this.targetPreviewPanel;
        if (!preview?.panel || !hoveredCharacter || !this.renderer?.domElement || !this.camera) {
            return;
        }

        const { x, y } = this.getWorldPositionForCell(hoveredCharacter.gridX, hoveredCharacter.gridY);
        const worldPoint = new THREE.Vector3(x, y, 0);
        worldPoint.project(this.camera);

        const rendererRect = this.renderer.domElement.getBoundingClientRect();
        const containerRect = this.container.getBoundingClientRect();

        const ndcToPixelX = (worldPoint.x * 0.5 + 0.5) * rendererRect.width;
        const ndcToPixelY = (-worldPoint.y * 0.5 + 0.5) * rendererRect.height;
        const anchorX = (rendererRect.left - containerRect.left) + ndcToPixelX;
        const anchorY = (rendererRect.top - containerRect.top) + ndcToPixelY;

        const cellPixelSize = rendererRect.width / Math.max(1, this.viewWidth);
        const verticalGap = (cellPixelSize * 0.5) + 10;
        const panelWidth = preview.panel.offsetWidth;
        const panelHeight = preview.panel.offsetHeight;

        const minLeft = 8;
        const maxLeft = Math.max(minLeft, this.container.clientWidth - panelWidth - 8);
        const minTop = 8;
        const maxTop = Math.max(minTop, this.container.clientHeight - panelHeight - 8);

        let left = anchorX - (panelWidth / 2);
        let top = anchorY - panelHeight - verticalGap;

        if (top < minTop) {
            top = anchorY + verticalGap;
        }

        left = Math.min(maxLeft, Math.max(minLeft, left));
        top = Math.min(maxTop, Math.max(minTop, top));

        preview.panel.style.left = `${left}px`;
        preview.panel.style.top = `${top}px`;
    },

    updateTargetPreview(activeCharacter) {
        const preview = this.targetPreviewPanel;
        if (!preview) {
            return;
        }

        const selectedAbility = activeCharacter ? this.getAbilityForCharacter(activeCharacter) : null;
        const hoveredCharacter = this.hoveredCharacter;
        const targetInfo = activeCharacter && hoveredCharacter && selectedAbility
            ? this.getExpectedActionEffect(activeCharacter, hoveredCharacter, selectedAbility)
            : null;

        if (
            !activeCharacter ||
            activeCharacter.isDead ||
            activeCharacter.team !== 'player' ||
            !hoveredCharacter ||
            !selectedAbility ||
            selectedAbility.type === 'buff' ||
            !targetInfo
        ) {
            preview.panel.style.display = 'none';
            return;
        }

        preview.panel.style.display = 'block';
        preview.title.textContent = `${hoveredCharacter.name} • ${selectedAbility.name}`;
        preview.title.style.color = hoveredCharacter.accentColor;
        preview.subtitle.textContent = targetInfo.isValid
            ? `In Range • ${targetInfo.distance} tile${targetInfo.distance === 1 ? '' : 's'}`
            : !targetInfo.correctTeam
                ? 'Invalid Target'
                : !targetInfo.hasLineOfSight
                    ? 'Blocked By Wall'
                : !targetInfo.withinRange
                    ? `Out of Range • ${targetInfo.distance} tile${targetInfo.distance === 1 ? '' : 's'}`
                    : !targetInfo.canAfford
                        ? 'Not Enough MP'
                        : 'No Actions Remaining';
        preview.subtitle.style.color = targetInfo.isValid ? '#8ee7a8' : '#ff9f9f';

        const targetHpText = `${hoveredCharacter.hitPoints} / ${hoveredCharacter.maxHitPoints} HP`;
        if (targetInfo.effectType === 'heal') {
            preview.effect.textContent = `Restore ${targetInfo.amount} HP`;
            preview.effect.style.color = '#9af0c0';
            preview.detail.textContent = `${targetHpText} • ${Math.max(0, hoveredCharacter.maxHitPoints - hoveredCharacter.hitPoints)} missing HP.`;
            this.positionTargetPreviewPanel(hoveredCharacter);
            return;
        }

        if (targetInfo.effectType === 'damage') {
            preview.effect.textContent = `Deal ${targetInfo.amount} damage`;
            preview.effect.style.color = targetInfo.amount >= hoveredCharacter.hitPoints ? '#ffd470' : '#f0e8d2';
            preview.detail.textContent = `${targetHpText} • AC ${hoveredCharacter.armorClass} • ${targetInfo.damageKind}.`;
            this.positionTargetPreviewPanel(hoveredCharacter);
            return;
        }

        preview.effect.textContent = targetInfo.description;
        preview.effect.style.color = '#f0e8d2';
        preview.detail.textContent = targetHpText;
        this.positionTargetPreviewPanel(hoveredCharacter);
    },

    setupLootMenuWindow() {
        if (this.lootMenuWindow?.overlay) {
            return;
        }

        const overlay = document.createElement('div');
        overlay.style.position = 'absolute';
        overlay.style.inset = '0';
        overlay.style.display = 'none';
        overlay.style.alignItems = 'center';
        overlay.style.justifyContent = 'center';
        overlay.style.background = 'rgba(4, 5, 8, 0.72)';
        overlay.style.backdropFilter = 'blur(4px)';
        overlay.style.pointerEvents = 'auto';
        overlay.style.zIndex = '34';

        const panel = document.createElement('div');
        panel.style.width = 'min(460px, calc(100% - 32px))';
        panel.style.maxHeight = 'min(520px, calc(100% - 32px))';
        panel.style.display = 'flex';
        panel.style.flexDirection = 'column';
        panel.style.borderRadius = '12px';
        panel.style.border = '1px solid rgba(232, 224, 202, 0.22)';
        panel.style.background = 'linear-gradient(180deg, rgba(25, 24, 28, 0.98), rgba(12, 12, 14, 0.98))';
        panel.style.boxShadow = '0 20px 50px rgba(0, 0, 0, 0.45), inset 0 0 0 1px rgba(255, 255, 255, 0.04)';
        panel.style.overflow = 'hidden';

        const header = document.createElement('div');
        header.style.display = 'flex';
        header.style.alignItems = 'center';
        header.style.justifyContent = 'space-between';
        header.style.gap = '12px';
        header.style.padding = '14px 16px 10px';
        header.style.borderBottom = '1px solid rgba(255, 255, 255, 0.08)';

        const titleWrap = document.createElement('div');
        const title = document.createElement('div');
        title.style.fontSize = '18px';
        title.style.fontWeight = '700';
        title.style.color = '#f0e8d2';
        title.textContent = 'Loot Bag';

        const subtitle = document.createElement('div');
        subtitle.style.marginTop = '4px';
        subtitle.style.fontSize = '11px';
        subtitle.style.letterSpacing = '0.08em';
        subtitle.style.textTransform = 'uppercase';
        subtitle.style.color = '#a89c82';

        titleWrap.appendChild(title);
        titleWrap.appendChild(subtitle);

        const closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.textContent = 'Close';
        closeButton.style.padding = '7px 10px';
        closeButton.style.borderRadius = '6px';
        closeButton.style.border = '1px solid rgba(255,255,255,0.16)';
        closeButton.style.background = 'rgba(255,255,255,0.06)';
        closeButton.style.color = '#f0e8d2';
        closeButton.style.fontSize = '11px';
        closeButton.style.cursor = 'pointer';
        closeButton.addEventListener('click', () => this.closeLootMenu());

        header.appendChild(titleWrap);
        header.appendChild(closeButton);
        panel.appendChild(header);

        const content = document.createElement('div');
        content.style.padding = '14px 16px 16px';
        content.style.overflowY = 'auto';
        content.style.minHeight = '180px';
        panel.appendChild(content);

        overlay.addEventListener('click', (event) => {
            if (event.target === overlay) {
                this.closeLootMenu();
            }
        });

        panel.addEventListener('click', (event) => event.stopPropagation());
        overlay.appendChild(panel);
        this.container.appendChild(overlay);

        this.lootMenuWindow = {
            overlay,
            panel,
            title,
            subtitle,
            content,
            closeButton
        };
    },

    openLootMenu(cellKey) {
        if (!this.lootMenuWindow) {
            this.setupLootMenuWindow();
        }

        this.activeLootCellKey = cellKey;
        this.lootMenuWindow.overlay.style.display = 'flex';
        this.renderLootMenuForCell(cellKey);
    },

    closeLootMenu() {
        if (!this.lootMenuWindow) {
            return;
        }

        this.lootMenuWindow.overlay.style.display = 'none';
        this.activeLootCellKey = null;
    },

    renderLootMenuForCell(cellKey) {
        const modal = this.lootMenuWindow;
        if (!modal || !cellKey) {
            return;
        }

        const drop = this.lootDropsByCell.get(cellKey);
        if (!drop) {
            this.closeLootMenu();
            return;
        }

        const items = this.getLootMenuItemsForCell(cellKey);
        modal.title.textContent = 'Loot Bag';
        modal.subtitle.textContent = `Cell (${drop.gridX}, ${drop.gridY})`;
        modal.content.innerHTML = '';

        if (items.length === 0) {
            const empty = document.createElement('div');
            empty.style.padding = '12px';
            empty.style.border = '1px dashed rgba(255,255,255,0.18)';
            empty.style.borderRadius = '10px';
            empty.style.fontSize = '12px';
            empty.style.color = '#8f856f';
            empty.textContent = 'This bag is empty.';
            modal.content.appendChild(empty);
            return;
        }

        const info = document.createElement('div');
        info.style.marginBottom = '10px';
        info.style.fontSize = '12px';
        info.style.color = '#a89c82';
        info.textContent = 'Choose which items to take and which to leave on the ground.';
        modal.content.appendChild(info);

        const list = document.createElement('div');
        list.style.display = 'grid';
        list.style.gap = '8px';
        modal.content.appendChild(list);

        items.forEach((item) => {
            const row = document.createElement('div');
            row.style.display = 'flex';
            row.style.alignItems = 'center';
            row.style.justifyContent = 'space-between';
            row.style.gap = '10px';
            row.style.padding = '10px 12px';
            row.style.border = '1px solid rgba(255,255,255,0.08)';
            row.style.borderRadius = '10px';
            row.style.background = 'rgba(255,255,255,0.03)';

            const text = document.createElement('div');
            text.style.display = 'flex';
            text.style.flexDirection = 'column';
            text.style.minWidth = '0';

            const label = document.createElement('div');
            label.style.fontSize = '13px';
            label.style.fontWeight = '700';
            label.style.color = item.accentColor;
            label.textContent = item.label;

            const quantity = document.createElement('div');
            quantity.style.marginTop = '3px';
            quantity.style.fontSize = '11px';
            quantity.style.color = '#cfc4ae';
            quantity.textContent = `Quantity: ${item.quantity}`;

            text.appendChild(label);
            text.appendChild(quantity);
            row.appendChild(text);

            const controls = document.createElement('div');
            controls.style.display = 'flex';
            controls.style.gap = '6px';
            controls.style.flexShrink = '0';

            const takeButton = document.createElement('button');
            takeButton.type = 'button';
            takeButton.textContent = 'Take';
            takeButton.style.padding = '6px 9px';
            takeButton.style.border = '1px solid rgba(255,255,255,0.2)';
            takeButton.style.borderRadius = '6px';
            takeButton.style.background = 'rgba(78, 126, 54, 0.35)';
            takeButton.style.color = '#dff2d4';
            takeButton.style.fontSize = '11px';
            takeButton.style.cursor = 'pointer';
            takeButton.addEventListener('click', () => this.takeLootItem(cellKey, item.itemKey));

            const leaveButton = document.createElement('button');
            leaveButton.type = 'button';
            leaveButton.textContent = 'Leave';
            leaveButton.style.padding = '6px 9px';
            leaveButton.style.border = '1px solid rgba(255,255,255,0.2)';
            leaveButton.style.borderRadius = '6px';
            leaveButton.style.background = 'rgba(140, 104, 64, 0.28)';
            leaveButton.style.color = '#d9c7a8';
            leaveButton.style.fontSize = '11px';
            leaveButton.style.cursor = 'pointer';
            leaveButton.addEventListener('click', () => this.leaveLootItemOnGround(cellKey, item.itemKey));

            controls.appendChild(takeButton);
            controls.appendChild(leaveButton);
            row.appendChild(controls);
            list.appendChild(row);
        });
    },

    setupCharacterInventoryModal() {
        const overlay = document.createElement('div');
        overlay.style.position = 'absolute';
        overlay.style.inset = '0';
        overlay.style.display = 'none';
        overlay.style.alignItems = 'center';
        overlay.style.justifyContent = 'center';
        overlay.style.background = 'rgba(4, 5, 8, 0.72)';
        overlay.style.backdropFilter = 'blur(4px)';
        overlay.style.pointerEvents = 'auto';
        overlay.style.zIndex = '30';

        const panel = document.createElement('div');
        panel.style.position = 'absolute';
        panel.style.left = '50%';
        panel.style.top = '50%';
        panel.style.transform = 'translate(-50%, -50%)';
        panel.style.width = 'min(480px, calc(100% - 32px))';
        panel.style.maxHeight = 'min(520px, calc(100% - 32px))';
        panel.style.display = 'flex';
        panel.style.flexDirection = 'column';
        panel.style.borderRadius = '12px';
        panel.style.border = '1px solid rgba(232, 224, 202, 0.22)';
        panel.style.background = 'linear-gradient(180deg, rgba(25, 24, 28, 0.98), rgba(12, 12, 14, 0.98))';
        panel.style.boxShadow = '0 20px 50px rgba(0, 0, 0, 0.45), inset 0 0 0 1px rgba(255, 255, 255, 0.04)';
        panel.style.overflow = 'hidden';

        const header = document.createElement('div');
        header.style.display = 'flex';
        header.style.alignItems = 'center';
        header.style.justifyContent = 'space-between';
        header.style.gap = '12px';
        header.style.padding = '16px 18px 12px';
        header.style.borderBottom = '1px solid rgba(255, 255, 255, 0.08)';
        header.style.cursor = 'move';
        header.style.userSelect = 'none';

        const titleWrap = document.createElement('div');
        titleWrap.style.minWidth = '0';

        const title = document.createElement('div');
        title.style.fontSize = '18px';
        title.style.fontWeight = '700';
        title.style.color = '#f0e8d2';
        title.style.lineHeight = '1.2';

        const subtitle = document.createElement('div');
        subtitle.style.marginTop = '4px';
        subtitle.style.fontSize = '11px';
        subtitle.style.letterSpacing = '0.08em';
        subtitle.style.textTransform = 'uppercase';
        subtitle.style.color = '#a89c82';

        titleWrap.appendChild(title);
        titleWrap.appendChild(subtitle);

        const closeButton = document.createElement('button');
        closeButton.type = 'button';
        closeButton.textContent = 'Close';
        closeButton.style.padding = '7px 10px';
        closeButton.style.borderRadius = '6px';
        closeButton.style.border = '1px solid rgba(255,255,255,0.16)';
        closeButton.style.background = 'rgba(255,255,255,0.06)';
        closeButton.style.color = '#f0e8d2';
        closeButton.style.fontSize = '11px';
        closeButton.style.cursor = 'pointer';
        closeButton.addEventListener('click', () => this.closeCharacterInventory());

        header.appendChild(titleWrap);
        header.appendChild(closeButton);
        panel.appendChild(header);

        const tabBar = document.createElement('div');
        tabBar.style.display = 'flex';
        tabBar.style.gap = '8px';
        tabBar.style.padding = '12px 18px 0';

        const infoTabButton = document.createElement('button');
        infoTabButton.type = 'button';
        infoTabButton.textContent = 'Info';
        infoTabButton.style.padding = '8px 12px';
        infoTabButton.style.borderRadius = '8px 8px 0 0';
        infoTabButton.style.border = '1px solid rgba(255,255,255,0.16)';
        infoTabButton.style.cursor = 'pointer';
        infoTabButton.addEventListener('click', () => this.setCharacterInventoryTab('info'));

        const equipmentTabButton = document.createElement('button');
        equipmentTabButton.type = 'button';
        equipmentTabButton.textContent = 'Equipment';
        equipmentTabButton.style.padding = '8px 12px';
        equipmentTabButton.style.borderRadius = '8px 8px 0 0';
        equipmentTabButton.style.border = '1px solid rgba(255,255,255,0.16)';
        equipmentTabButton.style.cursor = 'pointer';
        equipmentTabButton.addEventListener('click', () => this.setCharacterInventoryTab('equipment'));

        const abilitiesTabButton = document.createElement('button');
        abilitiesTabButton.type = 'button';
        abilitiesTabButton.textContent = 'Abilities';
        abilitiesTabButton.style.padding = '8px 12px';
        abilitiesTabButton.style.borderRadius = '8px 8px 0 0';
        abilitiesTabButton.style.border = '1px solid rgba(255,255,255,0.16)';
        abilitiesTabButton.style.cursor = 'pointer';
        abilitiesTabButton.addEventListener('click', () => this.setCharacterInventoryTab('abilities'));

        const spellsTabButton = document.createElement('button');
        spellsTabButton.type = 'button';
        spellsTabButton.textContent = 'Spells';
        spellsTabButton.style.padding = '8px 12px';
        spellsTabButton.style.borderRadius = '8px 8px 0 0';
        spellsTabButton.style.border = '1px solid rgba(255,255,255,0.16)';
        spellsTabButton.style.cursor = 'pointer';
        spellsTabButton.addEventListener('click', () => this.setCharacterInventoryTab('spells'));

        const sharedLootTabButton = document.createElement('button');
        sharedLootTabButton.type = 'button';
        sharedLootTabButton.textContent = 'Shared Loot';
        sharedLootTabButton.style.padding = '8px 12px';
        sharedLootTabButton.style.borderRadius = '8px 8px 0 0';
        sharedLootTabButton.style.border = '1px solid rgba(255,255,255,0.16)';
        sharedLootTabButton.style.cursor = 'pointer';
        sharedLootTabButton.addEventListener('click', () => this.setCharacterInventoryTab('shared'));

        tabBar.appendChild(infoTabButton);
        tabBar.appendChild(equipmentTabButton);
        tabBar.appendChild(abilitiesTabButton);
        tabBar.appendChild(spellsTabButton);
        tabBar.appendChild(sharedLootTabButton);
        panel.appendChild(tabBar);

        const content = document.createElement('div');
        content.style.padding = '18px';
        content.style.overflowY = 'auto';
        content.style.minHeight = '260px';
        content.style.pointerEvents = 'auto';
        panel.appendChild(content);

        let suppressOverlayClose = false;

        overlay.addEventListener('click', (event) => {
            if (event.target === overlay && !suppressOverlayClose) {
                this.closeCharacterInventory();
            }

            suppressOverlayClose = false;
        });

        const startDrag = (event) => {
            if (event.target === closeButton) {
                return;
            }

            event.preventDefault();
            const rect = panel.getBoundingClientRect();
            const overlayRect = overlay.getBoundingClientRect();
            const offsetX = event.clientX - rect.left;
            const offsetY = event.clientY - rect.top;
            panel.style.left = `${rect.left - overlayRect.left}px`;
            panel.style.top = `${rect.top - overlayRect.top}px`;
            panel.style.transform = 'none';

            let hasMoved = false;

            const onMove = (moveEvent) => {
                hasMoved = true;
                const maxLeft = Math.max(8, overlay.clientWidth - panel.offsetWidth - 8);
                const maxTop = Math.max(8, overlay.clientHeight - panel.offsetHeight - 8);
                const nextLeft = Math.min(Math.max(8, moveEvent.clientX - overlayRect.left - offsetX), maxLeft);
                const nextTop = Math.min(Math.max(8, moveEvent.clientY - overlayRect.top - offsetY), maxTop);
                panel.style.left = `${nextLeft}px`;
                panel.style.top = `${nextTop}px`;
            };

            const onUp = () => {
                window.removeEventListener('pointermove', onMove);
                window.removeEventListener('pointerup', onUp);
                suppressOverlayClose = hasMoved;
            };

            window.addEventListener('pointermove', onMove);
            window.addEventListener('pointerup', onUp);
        };

        header.addEventListener('pointerdown', startDrag);

        panel.addEventListener('click', (event) => event.stopPropagation());
        overlay.appendChild(panel);
        this.container.appendChild(overlay);

        this.characterInventoryModal = {
            overlay,
            panel,
            title,
            subtitle,
            infoTabButton,
            equipmentTabButton,
            abilitiesTabButton,
            spellsTabButton,
            sharedLootTabButton,
            content,
            closeButton
        };
    },

    openCharacterInventory(character) {
        if (!character || character.team !== 'player' || !this.characterInventoryModal) {
            return;
        }

        this.activeInventoryCharacter = character;
        this.characterInventoryModal.overlay.style.display = 'flex';
        this.renderCharacterInventory();
    },

    closeCharacterInventory() {
        if (!this.characterInventoryModal) {
            return;
        }

        this.characterInventoryModal.overlay.style.display = 'none';
        this.activeInventoryCharacter = null;
    },

    setCharacterInventoryTab(tab) {
        if (tab !== 'info' && tab !== 'equipment' && tab !== 'abilities' && tab !== 'spells' && tab !== 'shared') {
            return;
        }

        this.activeInventoryTab = tab;
        this.renderCharacterInventory();
    },

    renderCharacterInventory() {
        const modal = this.characterInventoryModal;
        const character = this.activeInventoryCharacter;
        if (!modal || !character) {
            return;
        }

        modal.title.textContent = `${character.name} Lv ${character.level ?? 1}`;
        modal.title.style.color = character.accentColor;
        modal.subtitle.textContent = `${character.role} • ${character.race} • ${character.team}`;

        const isInfoTab = this.activeInventoryTab === 'info';
        const isEquipmentTab = this.activeInventoryTab === 'equipment';
        const isAbilitiesTab = this.activeInventoryTab === 'abilities';
        const isSpellsTab = this.activeInventoryTab === 'spells';
        const isSharedLootTab = this.activeInventoryTab === 'shared';
        this.setCharacterInventoryTabState(modal.infoTabButton, isInfoTab, character.accentColor);
        this.setCharacterInventoryTabState(modal.equipmentTabButton, isEquipmentTab, character.accentColor);
        this.setCharacterInventoryTabState(modal.abilitiesTabButton, isAbilitiesTab, character.accentColor);
        this.setCharacterInventoryTabState(modal.spellsTabButton, isSpellsTab, character.accentColor);
        this.setCharacterInventoryTabState(modal.sharedLootTabButton, isSharedLootTab, character.accentColor);

        if (isInfoTab) {
            this.renderCharacterInfoTab(character);
            return;
        }

        if (isEquipmentTab) {
            this.renderCharacterEquipmentTab(character);
            return;
        }

        if (isAbilitiesTab) {
            this.renderCharacterAbilitiesTab(character);
            return;
        }

        if (isSharedLootTab) {
            this.renderSharedLootTab(character);
            return;
        }

        this.renderCharacterSpellsTab(character);
    },

    renderSharedLootTab(character) {
        const modal = this.characterInventoryModal;
        if (!modal) {
            return;
        }

        const inventory = this.sharedLootInventory ?? {
            gold: 0,
            drops: []
        };

        modal.content.innerHTML = '';

        const intro = document.createElement('div');
        intro.style.marginBottom = '14px';
        intro.style.fontSize = '12px';
        intro.style.color = '#a89c82';
        intro.textContent = `Shared warband stash accessed by all heroes. ${character.name} can review the latest drops here.`;
        modal.content.appendChild(intro);

        const summaryGrid = document.createElement('div');
        summaryGrid.style.display = 'grid';
        summaryGrid.style.gridTemplateColumns = 'repeat(auto-fit, minmax(140px, 1fr))';
        summaryGrid.style.gap = '10px';
        modal.content.appendChild(summaryGrid);

        const summaryCards = [
            { label: 'Gold', value: String(inventory.gold ?? 0), color: '#ffd86a' },
            { label: 'Looted Enemies', value: String(inventory.drops?.length ?? 0), color: '#c6f3b3' }
        ];

        summaryCards.forEach((entry) => {
            const card = document.createElement('div');
            card.style.padding = '12px';
            card.style.border = '1px solid rgba(255,255,255,0.08)';
            card.style.borderRadius = '10px';
            card.style.background = 'rgba(255,255,255,0.03)';

            const label = document.createElement('div');
            label.style.fontSize = '10px';
            label.style.letterSpacing = '0.08em';
            label.style.textTransform = 'uppercase';
            label.style.color = '#8f856f';
            label.textContent = entry.label;

            const value = document.createElement('div');
            value.style.marginTop = '6px';
            value.style.fontSize = '24px';
            value.style.fontWeight = '700';
            value.style.color = entry.color;
            value.textContent = entry.value;

            card.appendChild(label);
            card.appendChild(value);
            summaryGrid.appendChild(card);
        });

        const historyTitle = document.createElement('div');
        historyTitle.style.marginTop = '14px';
        historyTitle.style.marginBottom = '8px';
        historyTitle.style.fontSize = '11px';
        historyTitle.style.letterSpacing = '0.08em';
        historyTitle.style.textTransform = 'uppercase';
        historyTitle.style.color = '#8f856f';
        historyTitle.textContent = 'Recent Drops';
        modal.content.appendChild(historyTitle);

        if (!inventory.drops || inventory.drops.length === 0) {
            const emptyState = document.createElement('div');
            emptyState.style.padding = '12px';
            emptyState.style.border = '1px dashed rgba(255,255,255,0.18)';
            emptyState.style.borderRadius = '10px';
            emptyState.style.fontSize = '12px';
            emptyState.style.color = '#8f856f';
            emptyState.textContent = 'No loot found yet.';
            modal.content.appendChild(emptyState);
            return;
        }

        const list = document.createElement('div');
        list.style.display = 'grid';
        list.style.gap = '8px';
        modal.content.appendChild(list);

        inventory.drops.slice(0, 12).forEach((drop) => {
            const row = document.createElement('div');
            row.style.padding = '10px 12px';
            row.style.border = '1px solid rgba(255,255,255,0.08)';
            row.style.borderRadius = '10px';
            row.style.background = 'rgba(255,255,255,0.03)';
            row.style.fontSize = '12px';
            row.style.color = '#f0e8d2';

            row.textContent = `${drop.enemyName} at (${drop.gridX}, ${drop.gridY}) dropped ${drop.gold} gold.`;
            list.appendChild(row);
        });
    },

    setCharacterInventoryTabState(button, isActive, accentColor) {
        button.style.background = isActive ? this.hexToRgba(accentColor, 0.22) : 'rgba(255,255,255,0.04)';
        button.style.borderColor = isActive ? this.hexToRgba(accentColor, 0.82) : 'rgba(255,255,255,0.14)';
        button.style.color = isActive ? '#f0e8d2' : '#a89c82';
    },

    renderCharacterInfoTab(character) {
        const modal = this.characterInventoryModal;
        if (!modal) {
            return;
        }

        const formatSigned = (value) => (value >= 0 ? `+${value}` : String(value));
        const strengthBonusHp = Math.max(0, (character.strength ?? 10) - 10);
        const strengthBonusDamage = Math.floor(strengthBonusHp / 2);
        const dexterityDiff = (character.dexterity ?? 10) - 10;
        const dexterityRangedDamageBonus = Math.floor(Math.max(0, dexterityDiff) / 2);
        const intelligenceBonus = Math.floor(Math.max(0, (character.intelligence ?? 10) - 10) / 2);
        const wisdomHealingBonus = Math.floor(Math.max(0, (character.wisdom ?? 10) - 10) / 2);

        const infoStats = [
            { label: 'Level', value: String(character.level ?? 1) },
            { label: 'Experience Points', value: String(character.experiencePoints ?? 0) },
            { label: 'Hit Points', value: `${character.hitPoints} / ${character.maxHitPoints}` },
            { label: 'Magic Points', value: `${character.magicPoints} / ${character.maxMagicPoints}` },
            { label: 'Initiative', value: String(character.initiative ?? 0) },
            { label: 'Damage', value: String(character.meleeAttackDamage) },
            { label: 'Armor Class', value: String(character.armorClass) },
            { label: 'Strength', value: String(character.strength), bonus: `HP +${strengthBonusHp}, Damage +${strengthBonusDamage}` },
            { label: 'Dexterity', value: String(character.dexterity), bonus: `Initiative ${formatSigned(dexterityDiff)}, Ranged Damage +${dexterityRangedDamageBonus}` },
            { label: 'Intelligence', value: String(character.intelligence), bonus: `MP Regen +${intelligenceBonus}, Spell Damage +${intelligenceBonus}` },
            { label: 'Wisdom', value: String(character.wisdom), bonus: `Healing +${wisdomHealingBonus}` },
            { label: 'Actions Per Turn', value: String(character.maxActionsPerTurn) },
            { label: 'Attack Cost', value: String(character.attackCost) },
            { label: 'MP Regen', value: String(character.mpRegen ?? 0) }
        ];

        modal.content.innerHTML = '';

        const infoGrid = document.createElement('div');
        infoGrid.style.display = 'grid';
        infoGrid.style.gridTemplateColumns = 'repeat(auto-fit, minmax(180px, 1fr))';
        infoGrid.style.gap = '10px';

        infoStats.forEach((stat) => {
            const statCard = document.createElement('div');
            statCard.style.padding = '12px';
            statCard.style.border = '1px solid rgba(255,255,255,0.08)';
            statCard.style.borderRadius = '8px';
            statCard.style.background = 'rgba(255,255,255,0.03)';

            const label = document.createElement('div');
            label.style.fontSize = '10px';
            label.style.letterSpacing = '0.08em';
            label.style.textTransform = 'uppercase';
            label.style.color = '#8f856f';
            label.textContent = stat.label;

            const value = document.createElement('div');
            value.style.marginTop = '6px';
            value.style.fontSize = '18px';
            value.style.fontWeight = '700';
            value.style.color = '#f0e8d2';
            value.textContent = stat.value;

            statCard.appendChild(label);
            statCard.appendChild(value);
            if (stat.bonus) {
                const bonus = document.createElement('div');
                bonus.style.marginTop = '4px';
                bonus.style.fontSize = '11px';
                bonus.style.fontWeight = '500';
                bonus.style.color = '#a89c82';
                bonus.textContent = stat.bonus;
                statCard.appendChild(bonus);
            }
            infoGrid.appendChild(statCard);
        });

        modal.content.appendChild(infoGrid);
    },

    renderCharacterEquipmentTab(character) {
        const modal = this.characterInventoryModal;
        if (!modal) {
            return;
        }

        const slotLabels = [
            ['head', 'Head'],
            ['body', 'Body'],
            ['hands', 'Hands'],
            ['legs', 'Legs'],
            ['feet', 'Feet'],
            ['neck', 'Neck']
        ];

        modal.content.innerHTML = '';

        const intro = document.createElement('div');
        intro.style.marginBottom = '14px';
        intro.style.fontSize = '12px';
        intro.style.color = '#a89c82';
        intro.textContent = `${character.name}'s equipment slots are ready for items.`;
        modal.content.appendChild(intro);

        const slotGrid = document.createElement('div');
        slotGrid.style.display = 'grid';
        slotGrid.style.gridTemplateColumns = 'repeat(auto-fit, minmax(130px, 1fr))';
        slotGrid.style.gap = '10px';

        slotLabels.forEach(([slotKey, slotLabel]) => {
            const slotCard = document.createElement('div');
            slotCard.style.minHeight = '92px';
            slotCard.style.padding = '12px';
            slotCard.style.border = '1px dashed rgba(255,255,255,0.16)';
            slotCard.style.borderRadius = '10px';
            slotCard.style.background = 'rgba(255,255,255,0.025)';
            slotCard.style.display = 'flex';
            slotCard.style.flexDirection = 'column';
            slotCard.style.justifyContent = 'space-between';

            const label = document.createElement('div');
            label.style.fontSize = '11px';
            label.style.letterSpacing = '0.08em';
            label.style.textTransform = 'uppercase';
            label.style.color = '#8f856f';
            label.textContent = slotLabel;

            const value = document.createElement('div');
            value.style.fontSize = '13px';
            value.style.fontWeight = '700';
            value.style.color = '#5e5648';
            value.textContent = character.equipment?.[slotKey] ?? 'Empty';

            slotCard.appendChild(label);
            slotCard.appendChild(value);
            slotGrid.appendChild(slotCard);
        });

        modal.content.appendChild(slotGrid);
    },

    renderCharacterAbilitiesTab(character) {
        const modal = this.characterInventoryModal;
        if (!modal) {
            return;
        }

        const nonSpellAbilities = (character.abilities ?? []).filter((ability) => ability.type !== 'spell' && ability.type !== 'heal');

        modal.content.innerHTML = '';

        const intro = document.createElement('div');
        intro.style.marginBottom = '14px';
        intro.style.fontSize = '12px';
        intro.style.color = '#a89c82';
        intro.textContent = 'Combat abilities currently known (spells excluded).';
        modal.content.appendChild(intro);

        if (nonSpellAbilities.length === 0) {
            const emptyState = document.createElement('div');
            emptyState.style.padding = '14px';
            emptyState.style.border = '1px dashed rgba(255,255,255,0.18)';
            emptyState.style.borderRadius = '10px';
            emptyState.style.color = '#8f856f';
            emptyState.style.fontSize = '12px';
            emptyState.textContent = 'No non-spell abilities available.';
            modal.content.appendChild(emptyState);
            return;
        }

        const abilityList = document.createElement('div');
        abilityList.style.display = 'grid';
        abilityList.style.gridTemplateColumns = 'repeat(auto-fit, minmax(220px, 1fr))';
        abilityList.style.gap = '10px';

        nonSpellAbilities.forEach((ability) => {
            const card = document.createElement('div');
            card.style.padding = '12px';
            card.style.border = '1px solid rgba(255,255,255,0.08)';
            card.style.borderRadius = '10px';
            card.style.background = 'rgba(255,255,255,0.03)';

            const name = document.createElement('div');
            name.style.fontSize = '14px';
            name.style.fontWeight = '700';
            name.style.color = '#f0e8d2';
            name.textContent = ability.name;

            const details = document.createElement('div');
            details.style.marginTop = '4px';
            details.style.fontSize = '11px';
            details.style.color = '#b5aa94';
            details.textContent = this.getAbilityDetailText(character, ability);

            const stats = document.createElement('div');
            stats.style.marginTop = '8px';
            stats.style.fontSize = '11px';
            stats.style.color = '#8f856f';
            const rangeText = `Range ${ability.range ?? 1}`;
            const costText = `Cost ${ability.mpCost ?? 0} MP`;
            const typeText = `Type ${ability.type}`;
            stats.textContent = `${typeText} • ${rangeText} • ${costText}`;

            const desc = document.createElement('div');
            desc.style.marginTop = '8px';
            desc.style.fontSize = '12px';
            desc.style.lineHeight = '1.45';
            desc.style.color = '#d6ccb7';
            desc.textContent = this.getAbilityDescriptionText(ability);

            card.appendChild(name);
            card.appendChild(details);
            card.appendChild(stats);
            card.appendChild(desc);
            abilityList.appendChild(card);
        });

        modal.content.appendChild(abilityList);
    },

    renderCharacterSpellsTab(character) {
        const modal = this.characterInventoryModal;
        if (!modal) {
            return;
        }

        const spells = (character.abilities ?? []).filter((ability) => ability.type === 'spell' || ability.type === 'heal');

        modal.content.innerHTML = '';

        const intro = document.createElement('div');
        intro.style.marginBottom = '14px';
        intro.style.fontSize = '12px';
        intro.style.color = '#a89c82';
        intro.textContent = 'Known spells and magical techniques.';
        modal.content.appendChild(intro);

        if (spells.length === 0) {
            const emptyState = document.createElement('div');
            emptyState.style.padding = '14px';
            emptyState.style.border = '1px dashed rgba(255,255,255,0.18)';
            emptyState.style.borderRadius = '10px';
            emptyState.style.color = '#8f856f';
            emptyState.style.fontSize = '12px';
            emptyState.textContent = 'No spells available.';
            modal.content.appendChild(emptyState);
            return;
        }

        const spellList = document.createElement('div');
        spellList.style.display = 'grid';
        spellList.style.gridTemplateColumns = 'repeat(auto-fit, minmax(220px, 1fr))';
        spellList.style.gap = '10px';

        spells.forEach((spell) => {
            const card = document.createElement('div');
            card.style.padding = '12px';
            card.style.border = '1px solid rgba(255,255,255,0.08)';
            card.style.borderRadius = '10px';
            card.style.background = 'rgba(255,255,255,0.03)';

            const name = document.createElement('div');
            name.style.fontSize = '14px';
            name.style.fontWeight = '700';
            name.style.color = '#f0e8d2';
            name.textContent = spell.name;

            const details = document.createElement('div');
            details.style.marginTop = '4px';
            details.style.fontSize = '11px';
            details.style.color = '#b5aa94';
            details.textContent = this.getAbilityDetailText(character, spell);

            const stats = document.createElement('div');
            stats.style.marginTop = '8px';
            stats.style.fontSize = '11px';
            stats.style.color = '#8f856f';
            const rangeText = `Range ${spell.range ?? 1}`;
            const costText = `Cost ${spell.mpCost ?? 0} MP`;
            const typeText = `Type ${spell.type}`;
            stats.textContent = `${typeText} • ${rangeText} • ${costText}`;

            const desc = document.createElement('div');
            desc.style.marginTop = '8px';
            desc.style.fontSize = '12px';
            desc.style.lineHeight = '1.45';
            desc.style.color = '#d6ccb7';
            desc.textContent = this.getAbilityDescriptionText(spell);

            card.appendChild(name);
            card.appendChild(details);
            card.appendChild(stats);
            card.appendChild(desc);
            spellList.appendChild(card);
        });

        modal.content.appendChild(spellList);
    },

    createPartySection(title, subtitle) {
        const section = document.createElement('section');
        section.style.marginBottom = '16px';

        const heading = document.createElement('div');
        heading.style.marginBottom = '10px';

        const titleText = document.createElement('div');
        titleText.style.fontSize = '13px';
        titleText.style.fontWeight = 'bold';
        titleText.style.letterSpacing = '0.08em';
        titleText.style.textTransform = 'uppercase';
        titleText.style.color = '#e3d6bb';
        titleText.textContent = title;

        const subtitleText = document.createElement('div');
        subtitleText.style.fontSize = '10px';
        subtitleText.style.color = '#8d8169';
        subtitleText.style.marginTop = '2px';
        subtitleText.textContent = subtitle;

        heading.appendChild(titleText);
        heading.appendChild(subtitleText);
        section.appendChild(heading);

        const content = document.createElement('div');
        section.appendChild(content);

        return { section, content };
    },

    createCombatCard(character) {
        const card = document.createElement('div');
        card.style.marginBottom = '0';
        card.style.width = '100%';
        card.style.padding = '6px';
        card.style.border = `1px solid ${character.accentColor}`;
        card.style.borderRadius = '6px';
        card.style.background = 'linear-gradient(180deg, rgba(22, 22, 20, 0.94), rgba(12, 12, 12, 0.94))';
        card.style.boxShadow = `inset 0 0 0 1px ${this.hexToRgba(character.accentColor, 0.15)}`;
        card.style.transition = 'box-shadow 140ms ease, border-color 140ms ease, background 140ms ease, transform 140ms ease';
        card.style.pointerEvents = 'auto';
        card.style.cursor = 'pointer';
        card.setAttribute('role', 'button');
        card.setAttribute('tabindex', '0');
        card.setAttribute('aria-label', `Focus camera on ${character.name}`);
        card.dataset.characterId = character.id;

        const topRow = document.createElement('div');
        topRow.style.display = 'flex';
        topRow.style.alignItems = 'center';
        topRow.style.gap = '6px';
        topRow.style.marginBottom = '6px';

        const portraitFrame = document.createElement('div');
        portraitFrame.style.width = '16px';
        portraitFrame.style.height = '16px';
        portraitFrame.style.flex = '0 0 16px';
        portraitFrame.style.borderRadius = '3px';
        portraitFrame.style.overflow = 'hidden';
        portraitFrame.style.border = `1px solid ${this.hexToRgba(character.accentColor, 0.30)}`;
        portraitFrame.style.boxShadow = `0 0 0 1px ${this.hexToRgba(character.accentColor, 0.22)}`;
        portraitFrame.style.transition = 'box-shadow 140ms ease, border-color 140ms ease, transform 140ms ease';
        portraitFrame.style.pointerEvents = character.team === 'player' ? 'auto' : 'none';
        portraitFrame.style.cursor = character.team === 'player' ? 'pointer' : 'default';
        portraitFrame.appendChild(this.createPortraitCanvas(character.spriteFrame, character.accentColor));
        if (character.team === 'player') {
            portraitFrame.title = `Open ${character.name} inventory`;
            portraitFrame.setAttribute('role', 'button');
            portraitFrame.setAttribute('tabindex', '0');
            portraitFrame.setAttribute('aria-label', `Open ${character.name} inventory`);
            portraitFrame.addEventListener('click', () => {
                this.openCharacterInventory(character);
            });
            portraitFrame.addEventListener('keydown', (event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    this.openCharacterInventory(character);
                }
            });
        }

        const portraitCol = document.createElement('div');
        portraitCol.style.display = 'flex';
        portraitCol.style.flexDirection = 'column';
        portraitCol.style.alignItems = 'center';
        portraitCol.style.flex = '0 0 auto';
        portraitCol.appendChild(portraitFrame);

        const textColumn = document.createElement('div');
        textColumn.style.minWidth = '0';
        textColumn.style.flex = '1';
        textColumn.style.display = 'flex';
        textColumn.style.flexDirection = 'column';

        const nameRow = document.createElement('div');
        nameRow.style.display = 'flex';
        nameRow.style.alignItems = 'baseline';
        nameRow.style.justifyContent = 'space-between';
        nameRow.style.gap = '6px';

        const nameText = document.createElement('div');
        nameText.style.fontSize = '12px';
        nameText.style.fontWeight = 'bold';
        nameText.style.lineHeight = '1.15';
        nameText.style.color = character.accentColor;
        nameText.textContent = `${character.name} Lv ${character.level ?? 1}`;

        const acBadge = document.createElement('div');
        acBadge.style.fontSize = '7px';
        acBadge.style.lineHeight = '1';
        acBadge.style.color = '#c8c0a8';
        acBadge.style.letterSpacing = '0.05em';
        acBadge.style.flexShrink = '0';
        acBadge.title = 'Armor Class — reduces physical damage';
        acBadge.textContent = `AC ${character.armorClass}`;

        const teamBadge = document.createElement('div');
        teamBadge.style.fontSize = '7px';
        teamBadge.style.lineHeight = '1';
        teamBadge.style.letterSpacing = '0.06em';
        teamBadge.style.textTransform = 'uppercase';
        teamBadge.style.padding = '2px 5px';
        teamBadge.style.borderRadius = '999px';
        teamBadge.style.border = `1px solid ${this.hexToRgba(character.accentColor, 0.6)}`;
        teamBadge.style.background = this.hexToRgba(character.accentColor, 0.18);
        teamBadge.style.color = '#efe5d0';
        teamBadge.style.flexShrink = '0';
        teamBadge.textContent = character.team === 'player' ? 'Player' : 'Enemy';

        const turnBadge = document.createElement('div');
        turnBadge.style.fontSize = '7px';
        turnBadge.style.lineHeight = '1';
        turnBadge.style.letterSpacing = '0.06em';
        turnBadge.style.textTransform = 'uppercase';
        turnBadge.style.padding = '2px 5px';
        turnBadge.style.borderRadius = '999px';
        turnBadge.style.border = '1px solid rgba(255,255,255,0.18)';
        turnBadge.style.background = 'rgba(255,255,255,0.04)';
        turnBadge.style.color = '#bcb29c';
        turnBadge.style.flexShrink = '0';
        turnBadge.textContent = 'Waiting';

        const metaBadgeRow = document.createElement('div');
        metaBadgeRow.style.display = 'flex';
        metaBadgeRow.style.alignItems = 'center';
        metaBadgeRow.style.gap = '4px';
        metaBadgeRow.style.marginTop = '4px';
        metaBadgeRow.appendChild(teamBadge);
        metaBadgeRow.appendChild(turnBadge);

        nameRow.appendChild(nameText);
        nameRow.appendChild(acBadge);
        textColumn.appendChild(nameRow);
        textColumn.appendChild(metaBadgeRow);
        topRow.appendChild(portraitCol);
        topRow.appendChild(textColumn);
        card.appendChild(topRow);

        const hpLabelRow = document.createElement('div');
        hpLabelRow.style.display = 'flex';
        hpLabelRow.style.justifyContent = 'space-between';
        hpLabelRow.style.fontSize = '9px';
        hpLabelRow.style.color = '#bcb29c';
        hpLabelRow.style.marginBottom = '3px';

        const hpLabel = document.createElement('div');
        hpLabel.textContent = 'HP';

        const hpText = document.createElement('div');
        hpText.style.color = '#f0e8d2';

        hpLabelRow.appendChild(hpLabel);
        hpLabelRow.appendChild(hpText);
        card.appendChild(hpLabelRow);

        const hpTrack = document.createElement('div');
        hpTrack.style.height = '6px';
        hpTrack.style.marginBottom = '6px';
        hpTrack.style.border = '1px solid rgba(255,255,255,0.12)';
        hpTrack.style.borderRadius = '999px';
        hpTrack.style.background = 'rgba(0, 0, 0, 0.42)';
        hpTrack.style.overflow = 'hidden';

        const hpFill = document.createElement('div');
        hpFill.style.height = '100%';
        hpFill.style.width = '100%';
        hpFill.style.background = 'linear-gradient(90deg, #b32626, rgba(179, 38, 38, 0.55))';
        hpTrack.appendChild(hpFill);
        card.appendChild(hpTrack);

        const mpLabelRow = document.createElement('div');
        mpLabelRow.style.display = 'flex';
        mpLabelRow.style.justifyContent = 'space-between';
        mpLabelRow.style.fontSize = '9px';
        mpLabelRow.style.color = '#bcb29c';
        mpLabelRow.style.marginBottom = '3px';

        const mpLabel = document.createElement('div');
        mpLabel.textContent = 'MP';

        const mpText = document.createElement('div');
        mpText.style.color = '#c8d8ff';

        mpLabelRow.appendChild(mpLabel);
        mpLabelRow.appendChild(mpText);
        card.appendChild(mpLabelRow);

        const mpTrack = document.createElement('div');
        mpTrack.style.height = '6px';
        mpTrack.style.marginBottom = '6px';
        mpTrack.style.border = '1px solid rgba(255,255,255,0.12)';
        mpTrack.style.borderRadius = '999px';
        mpTrack.style.background = 'rgba(0, 0, 0, 0.42)';
        mpTrack.style.overflow = 'hidden';

        const mpFill = document.createElement('div');
        mpFill.style.height = '100%';
        mpFill.style.width = '100%';
        mpFill.style.background = 'linear-gradient(90deg, #1f3f88, rgba(31, 63, 136, 0.55))';
        mpTrack.appendChild(mpFill);
        card.appendChild(mpTrack);

        const turnControls = document.createElement('div');
        turnControls.style.display = 'none';
        turnControls.style.marginTop = '4px';
        card.appendChild(turnControls);

        const actionText = document.createElement('div');
        actionText.style.marginBottom = '5px';
        actionText.style.fontSize = '10px';
        actionText.style.color = '#bcb29c';
        turnControls.appendChild(actionText);

        const abilityButtonMap = new Map();
        let endTurnButton = null;
        if (character.team === 'player') {
            const abilityBar = document.createElement('div');
            abilityBar.style.display = 'flex';
            abilityBar.style.alignItems = 'center';
            abilityBar.style.gap = '4px';
            abilityBar.style.flexWrap = 'wrap';
            abilityBar.style.justifyContent = 'space-between';
            abilityBar.style.pointerEvents = 'auto';

            const abilityButtons = document.createElement('div');
            abilityButtons.style.display = 'flex';
            abilityButtons.style.alignItems = 'center';
            abilityButtons.style.gap = '4px';
            abilityButtons.style.flexWrap = 'wrap';
            abilityButtons.style.flex = '1 1 auto';
            abilityBar.appendChild(abilityButtons);
            turnControls.appendChild(abilityBar);

            character.abilities.forEach((ability) => {
                const btn = document.createElement('button');
                btn.type = 'button';
                btn.style.position = 'relative';
                btn.style.display = 'inline-flex';
                btn.style.alignItems = 'center';
                btn.style.justifyContent = 'center';
                btn.style.width = '24px';
                btn.style.height = '24px';
                btn.style.flex = '0 0 24px';
                btn.style.padding = '0';
                btn.style.fontSize = '10px';
                btn.style.lineHeight = '1';
                btn.style.border = '1px solid rgba(255,255,255,0.18)';
                btn.style.borderRadius = '4px';
                btn.style.cursor = 'pointer';
                btn.style.background = 'rgba(0,0,0,0.45)';
                btn.style.color = '#bcb29c';
                btn.style.fontFamily = 'inherit';
                btn.style.textAlign = 'center';
                btn.style.transition = 'background 100ms ease, border-color 100ms ease, color 100ms ease, opacity 100ms ease';
                btn.style.pointerEvents = 'auto';
                const tooltipText = this.getAbilityTooltipText(character, ability);
                btn.title = tooltipText;
                btn.setAttribute('aria-label', tooltipText.replace(/\n/g, ', '));
                btn.innerHTML = this.getAbilityIconSvg(ability);

                if (ability.mpCost > 0) {
                    const costBadge = document.createElement('span');
                    costBadge.style.position = 'absolute';
                    costBadge.style.right = '2px';
                    costBadge.style.bottom = '1px';
                    costBadge.style.fontSize = '8px';
                    costBadge.style.lineHeight = '1';
                    costBadge.style.color = '#d7e4ff';
                    costBadge.style.textShadow = '0 0 4px rgba(0, 0, 0, 0.8)';
                    costBadge.textContent = String(ability.mpCost);
                    btn.appendChild(costBadge);
                }

                btn.addEventListener('click', () => {
                    if (this.getActiveTurnCharacter() !== character || character.isDead) {
                        return;
                    }
                    const wasSelected = character.selectedAbilityId === ability.id;
                    character.selectedAbilityId = ability.id;

                    if (ability.type === 'buff' && wasSelected) {
                        if (ability.id === 'battle-shout') {
                            this.castBattleShout(character);
                        }
                    }
                });
                abilityButtonMap.set(ability.id, btn);
                abilityButtons.appendChild(btn);
            });

            endTurnButton = document.createElement('button');
            endTurnButton.type = 'button';
            endTurnButton.textContent = 'End Turn';
            endTurnButton.style.marginLeft = '6px';
            endTurnButton.style.padding = '4px 6px';
            endTurnButton.style.flex = '0 0 auto';
            endTurnButton.style.border = '1px solid rgba(255,255,255,0.18)';
            endTurnButton.style.borderRadius = '4px';
            endTurnButton.style.background = 'rgba(0,0,0,0.45)';
            endTurnButton.style.color = '#bcb29c';
            endTurnButton.style.fontSize = '9px';
            endTurnButton.style.lineHeight = '1';
            endTurnButton.style.fontFamily = 'inherit';
            endTurnButton.style.cursor = 'pointer';
            endTurnButton.style.pointerEvents = 'auto';
            endTurnButton.style.transition = 'background 100ms ease, border-color 100ms ease, color 100ms ease, opacity 100ms ease';
            endTurnButton.setAttribute('aria-label', `End ${character.name}'s turn`);
            endTurnButton.addEventListener('click', () => {
                if (this.getActiveTurnCharacter() !== character || character.isDead) {
                    return;
                }

                this.endCurrentTurn();
            });
            abilityBar.appendChild(endTurnButton);
        }

        const focusCardCharacter = () => {
            this.focusCameraOnCharacter(character, 1800);
        };

        const clearMarchingDropHint = () => {
            card.style.outline = 'none';
            card.style.outlineOffset = '0';
        };

        if (character.team === 'player') {
            card.setAttribute('draggable', 'true');
            card.title = `${character.name} (drag to reorder marching order in exploration)`;

            card.addEventListener('dragstart', (event) => {
                if (this.gameMode !== 'exploration' || character.isDead) {
                    event.preventDefault();
                    return;
                }

                this.draggedMarchCharacterId = character.id;
                event.dataTransfer.effectAllowed = 'move';
                event.dataTransfer.setData('text/plain', character.id);
                card.style.opacity = '0.6';
            });

            card.addEventListener('dragend', () => {
                this.draggedMarchCharacterId = null;
                card.style.opacity = '1';
                clearMarchingDropHint();
            });

            card.addEventListener('dragover', (event) => {
                if (this.gameMode !== 'exploration' || character.isDead) {
                    return;
                }

                const draggedCharacterId = this.draggedMarchCharacterId;
                if (!draggedCharacterId || draggedCharacterId === character.id) {
                    return;
                }

                event.preventDefault();
                event.dataTransfer.dropEffect = 'move';
                card.style.outline = `1px dashed ${this.hexToRgba(character.accentColor, 0.75)}`;
                card.style.outlineOffset = '1px';
            });

            card.addEventListener('dragleave', clearMarchingDropHint);

            card.addEventListener('drop', (event) => {
                event.preventDefault();
                clearMarchingDropHint();

                if (this.gameMode !== 'exploration' || character.isDead) {
                    return;
                }

                const draggedCharacterId = this.draggedMarchCharacterId || event.dataTransfer.getData('text/plain');
                this.reorderExplorationMarchingOrder(draggedCharacterId, character.id);
            });
        }

        card.addEventListener('click', (event) => {
            const eventTarget = event.target instanceof Element ? event.target : null;
            if (eventTarget?.closest('button')) {
                return;
            }
            if (eventTarget && portraitFrame.contains(eventTarget)) {
                return;
            }

            if (this.gameMode === 'exploration' && character.team === 'player' && !character.isDead) {
                this.setExplorationLeadCharacter(character);
            }

            focusCardCharacter();
        });

        card.addEventListener('keydown', (event) => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                focusCardCharacter();
            }
        });

        return { card, portraitFrame, nameText, hpText, hpFill, mpText, mpFill, actionText, abilityButtonMap, acBadge, teamBadge, turnBadge, endTurnButton, turnControls };
    },

    setCombatCardActiveState(card, portraitFrame, accentColor, isActiveTurn, isDead) {
        if (isDead) {
            card.style.borderColor = '#5b5b5b';
            card.style.background = 'linear-gradient(180deg, rgba(26, 26, 26, 0.9), rgba(16, 16, 16, 0.9))';
            card.style.boxShadow = 'inset 0 0 0 1px rgba(120, 120, 120, 0.10)';
            card.style.transform = 'translateX(0)';
            portraitFrame.style.borderColor = '#5b5b5b';
            portraitFrame.style.boxShadow = '0 0 0 1px rgba(120, 120, 120, 0.14)';
            portraitFrame.style.transform = 'scale(1)';
            return;
        }

        if (isActiveTurn) {
            card.style.borderColor = accentColor;
            card.style.background = `linear-gradient(180deg, ${this.hexToRgba(accentColor, 0.30)}, rgba(18, 18, 18, 0.96))`;
            card.style.boxShadow = `0 0 0 1px ${this.hexToRgba(accentColor, 0.34)}, 0 0 24px ${this.hexToRgba(accentColor, 0.24)}, inset 0 0 0 1px ${this.hexToRgba(accentColor, 0.20)}`;
            card.style.transform = 'translateX(4px)';
            portraitFrame.style.borderColor = accentColor;
            portraitFrame.style.boxShadow = `0 0 0 2px ${this.hexToRgba(accentColor, 0.55)}, 0 0 18px ${this.hexToRgba(accentColor, 0.50)}, inset 0 0 18px ${this.hexToRgba(accentColor, 0.14)}`;
            portraitFrame.style.transform = 'scale(1.02)';
            return;
        }

        card.style.borderColor = this.hexToRgba(accentColor, 0.7);
        card.style.background = 'linear-gradient(180deg, rgba(22, 22, 20, 0.94), rgba(12, 12, 12, 0.94))';
        card.style.boxShadow = `inset 0 0 0 1px ${this.hexToRgba(accentColor, 0.15)}`;
        card.style.transform = 'translateX(0)';
        portraitFrame.style.borderColor = this.hexToRgba(accentColor, 0.30);
        portraitFrame.style.boxShadow = `0 0 0 1px ${this.hexToRgba(accentColor, 0.22)}`;
        portraitFrame.style.transform = 'scale(1)';
    },

    updateCharacterCard(character, activeCharacter) {
        const hud = this.characterHud.get(character.id);
        if (!hud) {
            return;
        }

        const deadColor = '#666666';
        const aliveInfoColor = '#ffffff';
        const hpRatio = Math.max(0, character.hitPoints / character.maxHitPoints);
        const isActiveTurn = activeCharacter === character;

        hud.hpText.textContent = `${character.hitPoints} / ${character.maxHitPoints}`;
        if (hud.acBadge) hud.acBadge.textContent = `AC ${character.armorClass}`;
        hud.hpFill.style.width = `${hpRatio * 100}%`;
        hud.hpFill.style.opacity = character.isDead ? '0.35' : '1';

        const mpRatio = character.maxMagicPoints > 0 ? Math.max(0, character.magicPoints / character.maxMagicPoints) : 0;
        hud.mpText.textContent = `${character.magicPoints} / ${character.maxMagicPoints}`;
        hud.mpFill.style.width = `${mpRatio * 100}%`;
        hud.mpFill.style.opacity = character.isDead ? '0.35' : '1';
        hud.mpText.style.color = character.isDead ? deadColor : '#c8d8ff';

        hud.card.style.opacity = character.isDead ? '0.65' : '1';
        hud.nameText.textContent = `${character.name} Lv ${character.level ?? 1}`;
        hud.nameText.style.color = character.isDead ? deadColor : character.accentColor;

        hud.hpText.style.color = character.isDead ? deadColor : aliveInfoColor;

        if (character.isDead) {
            hud.actionText.textContent = 'Defeated';
            hud.actionText.style.color = deadColor;
        } else if (isActiveTurn) {
            hud.actionText.textContent = `${character.actionsRemaining} of ${character.maxActionsPerTurn} actions remaining`;
            hud.actionText.style.color = '#e1d6c1';
        } else {
            hud.actionText.textContent = `${character.maxActionsPerTurn} action turn on deck`;
            hud.actionText.style.color = '#bcb29c';
        }

        if (hud.turnControls) {
            hud.turnControls.style.display = isActiveTurn && !character.isDead ? 'block' : 'none';
        }

        this.setCombatCardActiveState(hud.card, hud.portraitFrame, character.accentColor, isActiveTurn, character.isDead);

        hud.abilityButtonMap.forEach((btn, abilityId) => {
            const ability = character.abilities.find((a) => a.id === abilityId);
            const isSelected = character.selectedAbilityId === abilityId;
            const isMyTurn = isActiveTurn && !character.isDead;
            const canAfford = ability.mpCost === 0 || character.magicPoints >= ability.mpCost;

            if (isSelected && isMyTurn && canAfford) {
                btn.style.background = this.hexToRgba(character.accentColor, 0.35);
                btn.style.borderColor = character.accentColor;
                btn.style.color = '#f0e8d2';
                btn.style.opacity = '1';
                btn.style.cursor = 'pointer';
            } else if (!isMyTurn || !canAfford) {
                btn.style.background = 'rgba(0,0,0,0.30)';
                btn.style.borderColor = 'rgba(255,255,255,0.08)';
                btn.style.color = '#5a5248';
                btn.style.opacity = '0.5';
                btn.style.cursor = 'default';
            } else {
                btn.style.background = 'rgba(0,0,0,0.45)';
                btn.style.borderColor = 'rgba(255,255,255,0.18)';
                btn.style.color = '#bcb29c';
                btn.style.opacity = '1';
                btn.style.cursor = 'pointer';
            }
        });

        if (hud.endTurnButton) {
            const canEndTurn = isActiveTurn && !character.isDead && character.team === 'player';
            hud.endTurnButton.disabled = !canEndTurn;

            if (canEndTurn) {
                hud.endTurnButton.style.background = this.hexToRgba(character.accentColor, 0.24);
                hud.endTurnButton.style.borderColor = this.hexToRgba(character.accentColor, 0.78);
                hud.endTurnButton.style.color = '#f0e8d2';
                hud.endTurnButton.style.opacity = '1';
                hud.endTurnButton.style.cursor = 'pointer';
            } else {
                hud.endTurnButton.style.background = 'rgba(0,0,0,0.30)';
                hud.endTurnButton.style.borderColor = 'rgba(255,255,255,0.08)';
                hud.endTurnButton.style.color = '#5a5248';
                hud.endTurnButton.style.opacity = '0.5';
                hud.endTurnButton.style.cursor = 'default';
            }
        }

        if (this.activeInventoryCharacter === character) {
            this.renderCharacterInventory();
        }
    },

    startVictorySequence() {
        if (this.isGameOver) {
            return;
        }

        this.isGameOver = true;
        this.gameOutcome = 'victory';
        this.victoryStartTime = performance.now();
        this.victoryText.textContent = 'Victory!';
        this.victoryText.style.color = '#ffd700';
        this.victoryText.style.textShadow = '0 0 16px rgba(255, 215, 0, 0.6), 0 0 32px rgba(255, 140, 0, 0.45)';
        this.victoryText.style.display = 'block';
        this.victoryText.style.opacity = '1';
    },

    startGameOverSequence() {
        if (this.isGameOver) {
            return;
        }

        this.isGameOver = true;
        this.gameOutcome = 'defeat';
        this.victoryStartTime = performance.now();
        this.victoryText.textContent = 'Game Over';
        this.victoryText.style.color = '#ff4d4d';
        this.victoryText.style.textShadow = '0 0 16px rgba(255, 77, 77, 0.6), 0 0 32px rgba(128, 0, 0, 0.45)';
        this.victoryText.style.display = 'block';
        this.victoryText.style.opacity = '1';
    },

    updateVictorySequence() {
        if (!this.isGameOver) {
            return;
        }

        const elapsedMs = performance.now() - this.victoryStartTime;
        const progress = Math.min(1, elapsedMs / this.victoryFadeDurationMs);
        this.victoryText.style.opacity = String(1 - progress);

        if (progress >= 1 && !this.restartTriggered) {
            this.restartTriggered = true;
            window.location.reload();
        }
    }
};
