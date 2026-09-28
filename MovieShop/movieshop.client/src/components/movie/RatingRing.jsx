import { useEffect, useRef, useState } from "react";
import { animate, motion, useInView } from "motion/react";

/**
 * Animált, kör alakú értékelésjelző (0–10). Nézetbe érve a kör kitöltődik,
 * a szám felpörög a végső értékig. Szín: zöld ≥ 7, borostyán ≥ 5, egyébként piros.
 */
export default function RatingRing({ value = 0, size = 76, stroke = 6 }) {
    const ref = useRef(null);
    const inView = useInView(ref, { once: true });
    const [display, setDisplay] = useState(0);

    const clamped = Math.max(0, Math.min(10, value));
    const radius = (size - stroke) / 2;
    const circumference = 2 * Math.PI * radius;
    const color = clamped >= 7 ? "var(--success)" : clamped >= 5 ? "var(--primary)" : "var(--destructive)";

    useEffect(() => {
        if (!inView) return;
        const controls = animate(0, clamped, {
            duration: 1.6,
            ease: [0.22, 1, 0.36, 1],
            onUpdate: (v) => setDisplay(v),
        });
        return () => controls.stop();
    }, [inView, clamped]);

    return (
        <div ref={ref} className="relative shrink-0" style={{ width: size, height: size }}>
            <svg width={size} height={size} className="-rotate-90">
                <circle
                    cx={size / 2}
                    cy={size / 2}
                    r={radius}
                    fill="none"
                    strokeWidth={stroke}
                    className="stroke-foreground/10"
                />
                <motion.circle
                    cx={size / 2}
                    cy={size / 2}
                    r={radius}
                    fill="none"
                    stroke={color}
                    strokeWidth={stroke}
                    strokeLinecap="round"
                    strokeDasharray={circumference}
                    initial={{ strokeDashoffset: circumference }}
                    animate={inView ? { strokeDashoffset: circumference * (1 - clamped / 10) } : undefined}
                    transition={{ duration: 1.6, ease: [0.22, 1, 0.36, 1] }}
                    style={{ filter: `drop-shadow(0 0 6px ${color})` }}
                />
            </svg>
            <span
                className="absolute inset-0 flex items-center justify-center font-bold tabular-nums"
                style={{ fontSize: size * 0.27 }}
            >
                {display.toFixed(1)}
            </span>
        </div>
    );
}
