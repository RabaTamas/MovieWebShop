import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { toast } from "sonner";

import AddressForm from "./AddressForm";

vi.mock("sonner", () => ({ toast: { error: vi.fn(), success: vi.fn() } }));

const fill = async (user, { street, city, zip }) => {
    if (street) await user.type(screen.getByLabelText("Street, house number"), street);
    if (city) await user.type(screen.getByLabelText("City"), city);
    if (zip) await user.type(screen.getByLabelText("Zip"), zip);
};

describe("AddressForm", () => {
    beforeEach(() => vi.mocked(toast.error).mockClear());

    it("hiányos címet nem ment el", async () => {
        const onSave = vi.fn();
        const user = userEvent.setup();
        render(<AddressForm title="Billing address" onSave={onSave} />);

        await fill(user, { street: "Fő utca 1.", city: "Budapest" });
        await user.click(screen.getByRole("button", { name: "Save" }));

        expect(onSave).not.toHaveBeenCalled();
        expect(toast.error).toHaveBeenCalledWith("Every field must be filled!");
    });

    it("nem négyjegyű irányítószámot elutasít", async () => {
        const onSave = vi.fn();
        const user = userEvent.setup();
        render(<AddressForm title="Billing address" onSave={onSave} />);

        await fill(user, { street: "Fő utca 1.", city: "Budapest", zip: "11A1" });
        await user.click(screen.getByRole("button", { name: "Save" }));

        expect(onSave).not.toHaveBeenCalled();
        expect(toast.error).toHaveBeenCalledWith("Zip must be 4 numbers");
    });

    it("érvényes címet elment és visszajelez", async () => {
        const onSave = vi.fn();
        const user = userEvent.setup();
        render(<AddressForm title="Billing address" onSave={onSave} />);

        await fill(user, { street: "Fő utca 1.", city: "Budapest", zip: "1111" });
        await user.click(screen.getByRole("button", { name: "Save" }));

        expect(onSave).toHaveBeenCalledWith({ street: "Fő utca 1.", city: "Budapest", zip: "1111" });
        expect(screen.getByText("Saved")).toBeInTheDocument();
    });

    it("a kezdeti címet előre kitölti", () => {
        render(
            <AddressForm
                title="Billing address"
                initialAddress={{ street: "Kossuth tér 2.", city: "Szeged", zip: "6720" }}
                onSave={() => {}}
            />
        );

        expect(screen.getByLabelText("City")).toHaveValue("Szeged");
        expect(screen.getByLabelText("Zip")).toHaveValue("6720");
    });
});
