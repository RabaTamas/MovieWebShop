import { Link } from "react-router-dom";

import Logo from "./Logo";

const Footer = () => (
    <footer className="mt-16 border-t bg-card/40">
        {/* Alsó / jobb oldali többlet-padding: a lebegő chatbot gomb ne takarja a tartalmat */}
        <div className="mx-auto flex max-w-7xl flex-col items-center justify-between gap-4 px-4 pt-8 pb-24 text-sm text-muted-foreground sm:flex-row sm:px-6 sm:pb-8 lg:pr-56 lg:pl-8">
            <Logo className="opacity-80" />
            <nav className="flex flex-wrap items-center justify-center gap-x-6 gap-y-2">
                <Link to="/" className="transition-colors hover:text-foreground">Home</Link>
                <Link to="/auctions" className="transition-colors hover:text-foreground">Auctions</Link>
                <Link to="/about" className="transition-colors hover:text-foreground">About</Link>
            </nav>
            <p>© {new Date().getFullYear()} MovieWebShop</p>
        </div>
    </footer>
);

export default Footer;
