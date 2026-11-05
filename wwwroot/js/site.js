document.addEventListener("DOMContentLoaded", () => {
    const filterButtons = document.querySelectorAll("[data-nexo-filter]");
    const searchInput = document.querySelector("[data-nexo-search]");
    const cards = document.querySelectorAll("[data-nexo-item]");
    const counter = document.querySelector("[data-nexo-counter]");
    const emptyFilterMessage = document.querySelector("[data-nexo-empty-filter]");

    if (!cards.length && !filterButtons.length && !searchInput) {
        return;
    }

    let activeType = "todo";

    const updateCatalog = () => {
        const query = (searchInput?.value || "").trim().toLowerCase();
        let visibleCount = 0;

        cards.forEach((card) => {
            const itemType = (card.getAttribute("data-nexo-type") || "").toLowerCase();
            const searchContent = (card.getAttribute("data-nexo-keywords") || "").toLowerCase();

            const matchesType = activeType === "todo" || itemType === activeType;
            const matchesSearch = !query || searchContent.includes(query);

            if (matchesType && matchesSearch) {
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

    if (searchInput) {
        searchInput.addEventListener("input", updateCatalog);
    }
});
