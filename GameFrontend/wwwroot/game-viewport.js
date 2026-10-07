(() => {
    const observers = new WeakMap();
    const minimapListeners = new WeakMap();
    window.tankViewport = {
        attach(board, receiver) {
            this.detach(board);
            const resize = () => {
                if (board.clientWidth > 0 && board.clientHeight > 0)
                    receiver.invokeMethodAsync('ResizeViewport', board.clientWidth, board.clientHeight).catch(() => {});
            };
            const observer = new ResizeObserver(resize);
            observer.observe(board);
            observers.set(board, observer);
            board.onkeydown = event => {
                if (['w', 'a', 's', 'd', 'arrowup', 'arrowleft', 'arrowdown', 'arrowright', ' '].includes(event.key.toLowerCase())) event.preventDefault();
            };
            resize();
        },
        detach(board) {
            observers.get(board)?.disconnect();
            observers.delete(board);
            board.onkeydown = null;
        },
        attachMinimap(dialog) {
            this.detachMinimap(dialog);
            const controller = new AbortController();
            minimapListeners.set(dialog, controller);
            document.addEventListener('keydown', event => {
                if (event.key !== 'Tab' || event.shiftKey || event.ctrlKey || event.altKey || event.metaKey) return;
                if (!dialog.isConnected || document.querySelector('dialog[open]') && !dialog.open) return;
                event.preventDefault();
                event.stopPropagation();
                if (!event.repeat) this.openSettings(dialog);
            }, { capture: true, signal: controller.signal });
        },
        detachMinimap(dialog) {
            minimapListeners.get(dialog)?.abort();
            minimapListeners.delete(dialog);
        },
        openSettings(dialog) {
            if (dialog.open) return this.closeSettings(dialog);
            dialog.onclose = () => document.querySelector('.game-board')?.focus({ preventScroll: true });
            dialog.onclick = event => {
                const rect = dialog.getBoundingClientRect();
                if (event.target === dialog && (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom)) dialog.close();
            };
            dialog.showModal();
        },
        closeSettings(dialog) { dialog.close(); }
    };
})();
