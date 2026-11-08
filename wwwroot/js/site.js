document.addEventListener("DOMContentLoaded", () => {
    const filterButtons = document.querySelectorAll("[data-nexo-filter]");
    const projectFilterButtons = document.querySelectorAll("[data-nexo-project-filter]");
    const searchInput = document.querySelector("[data-nexo-search]");
    const cards = document.querySelectorAll("[data-nexo-item]");
    const counter = document.querySelector("[data-nexo-counter]");
    const emptyFilterMessage = document.querySelector("[data-nexo-empty-filter]");

    if (!cards.length && !filterButtons.length && !projectFilterButtons.length && !searchInput) {
        return;
    }

    let activeType = "todo";
    let activeProjectScope = "todos";

    const updateCatalog = () => {
        const query = (searchInput?.value || "").trim().toLowerCase();
        let visibleCount = 0;

        cards.forEach((card) => {
            const itemType = (card.getAttribute("data-nexo-type") || "").toLowerCase();
            const itemOwner = (card.getAttribute("data-nexo-owner") || "").toLowerCase();
            const itemParticipating = card.getAttribute("data-nexo-participando") === "true";
            const searchContent = (card.getAttribute("data-nexo-keywords") || "").toLowerCase();

            const matchesType = activeType === "todo" || itemType === activeType;
            const matchesProjectScope =
                activeProjectScope === "todos" ||
                (activeProjectScope === "propios" && itemOwner === "propio") ||
                (activeProjectScope === "disponibles" && itemOwner === "otro" && !itemParticipating) ||
                (activeProjectScope === "participando" && itemParticipating);
            const matchesSearch = !query || searchContent.includes(query);

            if (matchesType && matchesProjectScope && matchesSearch) {
                card.style.display = "";
                visibleCount++;
            } else {
                card.style.display = "none";
            }
        });

        if (counter) {
            const singular = counter.getAttribute("data-singular") || "publicación";
            const plural = counter.getAttribute("data-plural") || "publicaciones";
            counter.textContent = `${visibleCount} ${visibleCount === 1 ? singular : plural}`;
        }

        if (emptyFilterMessage) {
            emptyFilterMessage.style.display = visibleCount === 0 ? "block" : "none";
        }
    };

    filterButtons.forEach((btn) => {
        btn.addEventListener("click", () => {
            filterButtons.forEach((b) => b.classList.remove("active"));
            btn.classList.add("active");
            activeType = (btn.getAttribute("data-nexo-filter") || "todo").toLowerCase();
            updateCatalog();
        });
    });

    projectFilterButtons.forEach((btn) => {
        btn.addEventListener("click", () => {
            projectFilterButtons.forEach((button) => {
                button.classList.remove("active");
                button.setAttribute("aria-pressed", "false");
            });
            btn.classList.add("active");
            btn.setAttribute("aria-pressed", "true");
            activeProjectScope = btn.getAttribute("data-nexo-project-filter") || "todos";
            updateCatalog();
        });
    });

    if (searchInput) {
        searchInput.addEventListener("input", updateCatalog);
    }
});
