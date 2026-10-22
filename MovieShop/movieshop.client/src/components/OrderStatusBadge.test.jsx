import { render, screen } from "@testing-library/react";

import OrderStatusBadge from "./OrderStatusBadge";

describe("OrderStatusBadge", () => {
    it.each([
        ["Completed", "text-success"],
        ["Pending", "text-primary"],
        ["Cancelled", "text-destructive"],
        ["Failed", "text-destructive"],
        ["Refunded", "text-muted-foreground"],
    ])("a(z) %s állapotot a megfelelő színnel jeleníti meg", (status, colorClass) => {
        render(<OrderStatusBadge status={status} />);

        expect(screen.getByText(status)).toHaveClass(colorClass);
    });

    it("a számként érkező enumot névre fordítja", () => {
        render(<OrderStatusBadge status={4} />);

        expect(screen.getByText("Completed")).toHaveClass("text-success");
    });

    it("ismeretlen számot változatlanul kiír", () => {
        render(<OrderStatusBadge status={99} />);

        expect(screen.getByText("99")).toBeInTheDocument();
    });
});
