import { Link } from "react-router-dom";
import { Clapperboard } from "lucide-react";

import { cn } from "@/lib/utils";

const Logo = ({ className, onClick }) => (
    <Link to="/" onClick={onClick} className={cn("group flex items-center gap-2", className)}>
        <span className="flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground shadow-[0_0_24px_-4px] shadow-primary/60 transition-transform group-hover:-rotate-6">
            <Clapperboard className="size-4.5" />
        </span>
        <span className="font-display text-2xl leading-none tracking-wide">
            Movie<span className="text-primary">WebShop</span>
        </span>
    </Link>
);

export default Logo;
