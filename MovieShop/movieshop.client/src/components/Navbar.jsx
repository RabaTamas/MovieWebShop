import { Link, NavLink } from "react-router-dom";
import { useState } from "react";
import {
    Clapperboard,
    FolderTree,
    Gavel,
    Home,
    Info,
    LogOut,
    MapPin,
    Menu,
    MessageSquareText,
    Moon,
    Package,
    ReceiptText,
    Settings2,
    ShieldCheck,
    ShoppingCart,
    Sparkles,
    Sun,
    Trophy,
    User,
    Users,
    Film,
} from "lucide-react";

import { useAuth } from "../contexts/AuthContext";
import { useTheme } from "../contexts/ThemeContext";
import { cn } from "@/lib/utils";
import Logo from "./Logo";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { Separator } from "@/components/ui/separator";

const mainLinks = [
    { to: "/", label: "Home", icon: Home, end: true },
    { to: "/about", label: "About", icon: Info },
    { to: "/auctions", label: "Auctions", icon: Gavel },
];

const adminLinks = [
    { to: "/admin/movies", label: "Manage Movies", icon: Film },
    { to: "/admin/categories", label: "Manage Categories", icon: FolderTree },
    { to: "/admin/users", label: "Manage Users", icon: Users },
    { to: "/admin/reviews", label: "Manage Reviews", icon: MessageSquareText },
    { to: "/admin/orders", label: "Manage Orders", icon: Package },
    { to: "/admin/carts", label: "Manage ShoppingCarts", icon: ShoppingCart },
    { to: "/admin/addresses", label: "Manage Addresses", icon: MapPin },
    { to: "/admin/auctions", label: "Manage Auctions", icon: Gavel },
];

const userLinks = [
    { to: "/profile", label: "Profile", icon: User },
    { to: "/orders", label: "Orders", icon: ReceiptText },
    { to: "/my-movies", label: "My Movies", icon: Clapperboard },
    { to: "/recommendations", label: "Recommendations", icon: Sparkles },
    { to: "/my-wins", label: "My Won Auctions", icon: Trophy },
];

const getInitials = (name) =>
    (name || "U")
        .split(/[\s@._-]+/)
        .filter(Boolean)
        .map((n) => n[0])
        .join("")
        .toUpperCase()
        .slice(0, 2);

const desktopLinkClass = ({ isActive }) =>
    cn(
        "relative rounded-md px-3 py-2 text-sm font-medium transition-colors hover:text-foreground",
        isActive ? "text-foreground" : "text-muted-foreground",
        isActive &&
            "after:absolute after:inset-x-3 after:-bottom-[13px] after:h-0.5 after:rounded-full after:bg-primary"
    );

const mobileLinkClass = ({ isActive }) =>
    cn(
        "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
        isActive ? "bg-accent text-accent-foreground" : "text-muted-foreground hover:bg-accent/60 hover:text-foreground"
    );

const ThemeToggle = () => {
    const { theme, toggleTheme } = useTheme();
    return (
        <Button
            variant="ghost"
            size="icon"
            onClick={toggleTheme}
            aria-label={theme === "dark" ? "Switch to light mode" : "Switch to dark mode"}
            title={theme === "dark" ? "Light mode" : "Dark mode"}
        >
            {theme === "dark" ? <Sun /> : <Moon />}
        </Button>
    );
};

const Navbar = () => {
    const { user, logout } = useAuth();
    const [mobileOpen, setMobileOpen] = useState(false);

    // Check if user is admin
    const isAdmin = user && user.role === "Admin";

    const closeMobile = () => setMobileOpen(false);

    return (
        <header className="sticky top-0 z-40 w-full border-b bg-background/80 backdrop-blur-xl supports-[backdrop-filter]:bg-background/60">
            <nav className="mx-auto flex h-16 max-w-7xl items-center gap-4 px-4 sm:px-6 lg:px-8">
                {/* Mobile menu */}
                <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
                    <SheetTrigger asChild>
                        <Button variant="ghost" size="icon" className="lg:hidden" aria-label="Toggle navigation">
                            <Menu />
                        </Button>
                    </SheetTrigger>
                    <SheetContent side="left" className="w-72 gap-0 overflow-y-auto">
                        <SheetHeader className="border-b">
                            <SheetTitle asChild>
                                <div>
                                    <Logo onClick={closeMobile} />
                                </div>
                            </SheetTitle>
                        </SheetHeader>
                        <div className="flex flex-col gap-1 p-3">
                            {mainLinks.map(({ to, label, icon: Icon, end }) => (
                                <NavLink key={to} to={to} end={end} className={mobileLinkClass} onClick={closeMobile}>
                                    <Icon className="size-4" />
                                    {label}
                                </NavLink>
                            ))}

                            {user && (
                                <>
                                    <Separator className="my-2" />
                                    <p className="px-3 py-1 text-xs font-medium uppercase tracking-wider text-muted-foreground">
                                        Account
                                    </p>
                                    <NavLink to="/cart" className={mobileLinkClass} onClick={closeMobile}>
                                        <ShoppingCart className="size-4" />
                                        My Cart
                                    </NavLink>
                                    {userLinks.map(({ to, label, icon: Icon }) => (
                                        <NavLink key={to} to={to} className={mobileLinkClass} onClick={closeMobile}>
                                            <Icon className="size-4" />
                                            {label}
                                        </NavLink>
                                    ))}
                                </>
                            )}

                            {isAdmin && (
                                <>
                                    <Separator className="my-2" />
                                    <p className="px-3 py-1 text-xs font-medium uppercase tracking-wider text-muted-foreground">
                                        Admin Panel
                                    </p>
                                    {adminLinks.map(({ to, label, icon: Icon }) => (
                                        <NavLink key={to} to={to} className={mobileLinkClass} onClick={closeMobile}>
                                            <Icon className="size-4" />
                                            {label}
                                        </NavLink>
                                    ))}
                                </>
                            )}

                            <Separator className="my-2" />
                            {!user ? (
                                <div className="grid grid-cols-2 gap-2 px-1">
                                    <Button variant="outline" asChild>
                                        <Link to="/login" onClick={closeMobile}>Login</Link>
                                    </Button>
                                    <Button asChild>
                                        <Link to="/register" onClick={closeMobile}>Register</Link>
                                    </Button>
                                </div>
                            ) : (
                                <Button
                                    variant="ghost"
                                    className="justify-start text-destructive hover:text-destructive"
                                    onClick={() => {
                                        closeMobile();
                                        logout();
                                    }}
                                >
                                    <LogOut />
                                    Logout
                                </Button>
                            )}
                        </div>
                    </SheetContent>
                </Sheet>

                <Logo />

                {/* Desktop links */}
                <div className="ml-6 hidden items-center gap-1 lg:flex">
                    {mainLinks.map(({ to, label, end }) => (
                        <NavLink key={to} to={to} end={end} className={desktopLinkClass}>
                            {label}
                        </NavLink>
                    ))}

                    {/* Admin Menu - Only visible for admin users */}
                    {isAdmin && (
                        <DropdownMenu modal={false}>
                            <DropdownMenuTrigger asChild>
                                <Button variant="ghost" size="sm" className="text-muted-foreground data-[state=open]:text-foreground">
                                    <ShieldCheck className="text-primary" />
                                    Admin Panel
                                </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="start" className="w-56">
                                <DropdownMenuLabel>Administration</DropdownMenuLabel>
                                {adminLinks.map(({ to, label, icon: Icon }) => (
                                    <DropdownMenuItem key={to} asChild>
                                        <Link to={to}>
                                            <Icon />
                                            {label}
                                        </Link>
                                    </DropdownMenuItem>
                                ))}
                            </DropdownMenuContent>
                        </DropdownMenu>
                    )}
                </div>

                {/* Right side */}
                <div className="ml-auto flex items-center gap-1 sm:gap-2">
                    <ThemeToggle />

                    {!user ? (
                        <>
                            <Button variant="ghost" asChild className="hidden sm:inline-flex">
                                <Link to="/login">Login</Link>
                            </Button>
                            <Button asChild>
                                <Link to="/register">Register</Link>
                            </Button>
                        </>
                    ) : (
                        <>
                            <Button variant="ghost" size="icon" asChild aria-label="My Cart" title="My Cart">
                                <Link to="/cart">
                                    <ShoppingCart />
                                </Link>
                            </Button>

                            {/* Settings Dropdown */}
                            <DropdownMenu modal={false}>
                                <DropdownMenuTrigger asChild>
                                    <Button variant="ghost" className="h-9 gap-2 px-1.5 sm:px-2" aria-label="Settings">
                                        <Avatar className="size-7 ring-1 ring-primary/40">
                                            <AvatarFallback className="bg-primary/15 text-[11px] text-primary">
                                                {getInitials(user.name || user.email)}
                                            </AvatarFallback>
                                        </Avatar>
                                        <span className="hidden max-w-32 truncate text-sm md:inline">{user.name || user.email?.split("@")[0] || "Settings"}</span>
                                        <Settings2 className="hidden text-muted-foreground md:block" />
                                    </Button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="end" className="w-60">
                                    <DropdownMenuLabel className="flex flex-col gap-0.5">
                                        <span className="truncate text-sm font-semibold text-foreground">{user.name}</span>
                                        {user.email && <span className="truncate font-normal">{user.email}</span>}
                                    </DropdownMenuLabel>
                                    <DropdownMenuSeparator />
                                    {userLinks.map(({ to, label, icon: Icon }) => (
                                        <DropdownMenuItem key={to} asChild>
                                            <Link to={to}>
                                                <Icon />
                                                {label}
                                            </Link>
                                        </DropdownMenuItem>
                                    ))}
                                    <DropdownMenuItem asChild>
                                        <Link to="/cart">
                                            <ShoppingCart />
                                            My Cart
                                        </Link>
                                    </DropdownMenuItem>
                                    <DropdownMenuSeparator />
                                    <DropdownMenuItem variant="destructive" onSelect={logout}>
                                        <LogOut />
                                        Logout
                                    </DropdownMenuItem>
                                </DropdownMenuContent>
                            </DropdownMenu>
                        </>
                    )}
                </div>
            </nav>
        </header>
    );
};

export default Navbar;
