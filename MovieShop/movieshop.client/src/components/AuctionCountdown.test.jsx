import { act, render, screen } from "@testing-library/react";

import AuctionCountdown from "./AuctionCountdown";

const NOW = new Date("2026-10-12T12:00:00Z");

describe("AuctionCountdown", () => {
    beforeEach(() => {
        vi.useFakeTimers();
        vi.setSystemTime(NOW);
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    const endsIn = (ms) => new Date(NOW.getTime() + ms).toISOString();

    it("óra felett órát, percet és másodpercet mutat", () => {
        render(<AuctionCountdown endsAt={endsIn(3_725_000)} />);

        expect(screen.getByText("1h 2m 5s")).toBeInTheDocument();
    });

    it("másodpercenként frissül", () => {
        render(<AuctionCountdown endsAt={endsIn(125_000)} />);
        expect(screen.getByText("2m 5s")).toBeInTheDocument();

        act(() => vi.advanceTimersByTime(3_000));

        expect(screen.getByText("2m 2s")).toBeInTheDocument();
    });

    it("az utolsó percben sürgősnek jelöli", () => {
        render(<AuctionCountdown endsAt={endsIn(30_000)} />);

        expect(screen.getByText("0m 30s")).toHaveClass("animate-pulse", "text-destructive");
    });

    it("a kiemelés kikapcsolható", () => {
        render(<AuctionCountdown endsAt={endsIn(30_000)} highlightUrgent={false} />);

        expect(screen.getByText("0m 30s")).not.toHaveClass("animate-pulse");
    });

    it("lejáratkor 'Ended' feliratot mutat és meghívja az onExpire-t", () => {
        const onExpire = vi.fn();
        render(<AuctionCountdown endsAt={endsIn(2_000)} onExpire={onExpire} />);

        act(() => vi.advanceTimersByTime(2_000));

        expect(screen.getByText("Ended")).toBeInTheDocument();
        expect(onExpire).toHaveBeenCalled();
    });
});
