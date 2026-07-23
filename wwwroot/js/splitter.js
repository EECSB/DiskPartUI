//Drag-to-resize splitters with localStorage persistence.
//A ".h-split" resizes the height of the element before it; a ".v-split" resizes the width.
//The resizable element carries data-splitkey for persistence.
window.splitter = {
    minSize: 80,

    init: function () {
        document.querySelectorAll(".h-split").forEach(function (handle) {
            window.splitter.attach(handle, "y");
        });

        document.querySelectorAll(".v-split").forEach(function (handle) {
            window.splitter.attach(handle, "x");
        });

        window.splitter.restore();
    },

    restore: function () {
        document.querySelectorAll("[data-splitkey]").forEach(function (target) {
            var saved = localStorage.getItem("dpui." + target.dataset.splitkey);
            if (saved) {
                target.style.flex = "0 0 " + saved + "px";
            }
        });
    },

    attach: function (handle, axis) {
        var target = handle.previousElementSibling;
        if (!target) {
            return;
        }

        var startPos = 0;
        var startSize = 0;
        var dragging = false;

        handle.addEventListener("pointerdown", function (e) {
            dragging = true;
            handle.setPointerCapture(e.pointerId);

            if (axis === "y") {
                startPos = e.clientY;
                startSize = target.offsetHeight;
            } else {
                startPos = e.clientX;
                startSize = target.offsetWidth;
            }

            e.preventDefault();
        });

        handle.addEventListener("pointermove", function (e) {
            if (!dragging) {
                return;
            }

            var current = 0;
            if (axis === "y") {
                current = e.clientY;
            } else {
                current = e.clientX;
            }

            var size = Math.max(window.splitter.minSize, startSize + (current - startPos));
            target.style.flex = "0 0 " + size + "px";
        });

        var end = function (e) {
            if (!dragging) {
                return;
            }

            dragging = false;
            handle.releasePointerCapture(e.pointerId);

            if (!target.dataset.splitkey) {
                return;
            }

            var size = 0;
            if (axis === "y") {
                size = target.offsetHeight;
            } else {
                size = target.offsetWidth;
            }

            localStorage.setItem("dpui." + target.dataset.splitkey, Math.round(size));
        };

        handle.addEventListener("pointerup", end);
        handle.addEventListener("pointercancel", end);
    }
};
