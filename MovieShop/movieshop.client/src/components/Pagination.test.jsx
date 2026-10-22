import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

import Pagination from "./Pagination";

const pageButtons = () =>
    screen
        .getAllByRole("button")
        .map((b) => b.textContent)
        .filter((t) => /^\d+$/.test(t));

describe("Pagination", () => {
    it("legfeljebb 7 oldalnál minden oldalszámot mutat", () => {
        render(<Pagination currentPage={1} totalPages={5} onPageChange={() => {}} />);

        expect(pageButtons()).toEqual(["1", "2", "3", "4", "5"]);
    });

    it("sok oldalnál kihagyással mutatja az aktuális környezetét", () => {
        render(<Pagination currentPage={6} totalPages={12} onPageChange={() => {}} />);

        expect(pageButtons()).toEqual(["1", "5", "6", "7", "12"]);
        expect(screen.getByRole("button", { name: "6" })).toHaveAttribute("aria-current", "page");
    });

    it("az első oldalon a vissza gomb le van tiltva", () => {
        render(<Pagination currentPage={1} totalPages={3} onPageChange={() => {}} />);

        expect(screen.getByRole("button", { name: "Previous page" })).toBeDisabled();
        expect(screen.getByRole("button", { name: "Next page" })).toBeEnabled();
    });

    it("a nyilak és az oldalszámok a megfelelő oldalt kérik", async () => {
        const onPageChange = vi.fn();
        const user = userEvent.setup();
        render(<Pagination currentPage={2} totalPages={3} onPageChange={onPageChange} />);

        await user.click(screen.getByRole("button", { name: "Next page" }));
        await user.click(screen.getByRole("button", { name: "Previous page" }));
        await user.click(screen.getByRole("button", { name: "3" }));

        expect(onPageChange.mock.calls).toEqual([[3], [1], [3]]);
    });

    it("kiírja a megjelenített tételek tartományát", () => {
        render(<Pagination currentPage={2} totalPages={3} totalItems={20} pageSize={8} onPageChange={() => {}} />);

        expect(screen.getByText("9–16")).toBeInTheDocument();
        expect(screen.getByText(/of 20/)).toBeInTheDocument();
    });

    it("nulla oldal esetén semmit nem renderel", () => {
        const { container } = render(<Pagination currentPage={1} totalPages={0} onPageChange={() => {}} />);

        expect(container).toBeEmptyDOMElement();
    });
});
