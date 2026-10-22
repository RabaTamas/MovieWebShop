import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";

import MovieCard from "./MovieCard";

const renderCard = (movie) =>
    render(
        <MemoryRouter>
            <MovieCard movie={movie} />
        </MemoryRouter>
    );

describe("MovieCard", () => {
    it("a film adatlapjára linkel, címmel és poszterrel", () => {
        renderCard({ id: 3, title: "Inception", price: 1299, imageUrl: "inception.jpg" });

        expect(screen.getByRole("link")).toHaveAttribute("href", "/movies/3");
        expect(screen.getByRole("heading", { name: "Inception" })).toBeInTheDocument();
        expect(screen.getByRole("img", { name: "Inception" })).toHaveAttribute("src", "inception.jpg");
    });

    it("kedvezmény nélkül csak a teljes árat mutatja", () => {
        renderCard({ id: 1, title: "Shawshank", price: 899 });

        expect(screen.getByText("899 Ft")).toBeInTheDocument();
        expect(screen.queryByText(/^-\d+%$/)).not.toBeInTheDocument();
    });

    it("kedvezménynél áthúzott eredeti árat, akciós árat és százalékot mutat", () => {
        renderCard({ id: 1, title: "The Matrix", price: 1000, discountedPrice: 800 });

        expect(screen.getByText("1000 Ft")).toHaveClass("line-through");
        expect(screen.getByText("800 Ft")).toBeInTheDocument();
        expect(screen.getByText("-20%")).toBeInTheDocument();
    });

    it("poszter nélkül helyettesítő ikont jelenít meg", () => {
        renderCard({ id: 1, title: "No Poster", price: 500, imageUrl: "" });

        expect(screen.queryByRole("img")).not.toBeInTheDocument();
    });
});
