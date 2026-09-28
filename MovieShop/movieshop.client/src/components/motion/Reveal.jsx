import { motion } from "motion/react";

const EASE = [0.22, 1, 0.36, 1];

/** Görgetéskor, a nézetbe érve felúszó és előtűnő blokk. */
export function Reveal({ children, className, delay = 0, y = 28, once = true }) {
    return (
        <motion.div
            className={className}
            initial={{ opacity: 0, y }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once, margin: "-60px" }}
            transition={{ duration: 0.7, delay, ease: EASE }}
        >
            {children}
        </motion.div>
    );
}

const containerVariants = {
    hidden: {},
    show: (stagger) => ({ transition: { staggerChildren: stagger } }),
};

const itemVariants = {
    hidden: { opacity: 0, y: 22, scale: 0.98 },
    show: { opacity: 1, y: 0, scale: 1, transition: { duration: 0.55, ease: EASE } },
};

/**
 * Lépcsőzetes előtűnés: a StaggerItem gyerekek egymás után animálnak be.
 * Később hozzáadott elemek (pl. lapozás után) is beúsznak.
 */
export function Stagger({ children, className, stagger = 0.07, as = "div", ...props }) {
    const Comp = motion[as] ?? motion.div;
    return (
        <Comp
            className={className}
            variants={containerVariants}
            custom={stagger}
            initial="hidden"
            whileInView="show"
            viewport={{ once: true, margin: "-40px" }}
            {...props}
        >
            {children}
        </Comp>
    );
}

export function StaggerItem({ children, className, as = "div", ...props }) {
    const Comp = motion[as] ?? motion.div;
    return (
        <Comp className={className} variants={itemVariants} {...props}>
            {children}
        </Comp>
    );
}
