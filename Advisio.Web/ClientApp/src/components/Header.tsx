import { Link, useLocation } from "react-router-dom"
import { Button } from "@/components/ui/button"
import { User, LogOut, Menu, X } from "lucide-react"
import { useState } from "react"
import { cn } from "@/lib/utils"

interface HeaderProps {
  isAuthenticated?: boolean
  username?: string
  isAdmin?: boolean
  onLogout?: () => void
}

const Header = ({
  isAuthenticated = false,
  username = "Пользователь",
  isAdmin = false,
  onLogout 
}: HeaderProps) => {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
  const location = useLocation()

  const navLinks = [
    { href: "/", label: "Главная" },
    { href: "/places", label: "Места" },
    { href: "/recommendations", label: "Рекомендации" },
    { href: "/survey", label: "Опрос" },
  ]

  const isActive = (path: string) => location.pathname === path

  return (
    <header className="sticky top-0 z-50 w-full border-b border-border/50 bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/80">
      <div className="container flex h-16 items-center justify-between">
        <Link 
          to="/" 
          className="flex items-center gap-2 text-2xl font-display font-bold text-foreground hover:text-primary transition-colors"
        >
          <span className="text-primary">Advisio</span>
        </Link>

        <nav className="hidden md:flex items-center gap-1">
          {navLinks.map((link) => (
            <Link
              key={link.href}
              to={link.href}
              className={cn(
                "px-4 py-2 rounded-lg font-medium transition-all duration-200",
                isActive(link.href)
                  ? "bg-primary/10 text-primary"
                  : "text-foreground/80 hover:text-foreground hover:bg-background-light"
              )}
            >
              {link.label}
            </Link>
          ))}
        </nav>

        <div className="hidden md:flex items-center gap-3">
          {isAuthenticated ? (
            <>
              <Link to="/profile">
                <Button variant="ghost" className="gap-2">
                  <User className="h-4 w-4" />
                  {username}
                </Button>
              </Link>
              <Button 
                variant="outline" 
                size="sm" 
                onClick={onLogout}
                className="gap-2"
              >
                <LogOut className="h-4 w-4" />
                Выйти
              </Button>
            </>
          ) : (
            <>
              <Link to="/login">
                <Button variant="ghost">Войти</Button>
              </Link>
              <Link to="/register">
                <Button variant="default">Регистрация</Button>
              </Link>
            </>
          )}
        </div>

        <Button
          variant="ghost"
          size="icon"
          className="md:hidden"
          onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
        >
          {mobileMenuOpen ? <X /> : <Menu />}
        </Button>
      </div>

      {mobileMenuOpen && (
        <div className="md:hidden border-t border-border/50 bg-background animate-slide-up">
          <nav className="container py-4 flex flex-col gap-2">
            {navLinks.map((link) => (
              <Link
                key={link.href}
                to={link.href}
                onClick={() => setMobileMenuOpen(false)}
                className={cn(
                  "px-4 py-3 rounded-lg font-medium transition-all",
                  isActive(link.href)
                    ? "bg-primary/10 text-primary"
                    : "text-foreground/80 hover:bg-background-light"
                )}
              >
                {link.label}
              </Link>
            ))}
            <div className="h-px bg-border my-2" />
            {isAuthenticated ? (
              <>
                <Link
                  to="/profile"
                  onClick={() => setMobileMenuOpen(false)}
                  className="px-4 py-3 rounded-lg font-medium text-foreground/80 hover:bg-background-light flex items-center gap-2"
                >
                  <User className="h-4 w-4" />
                  {username}
                </Link>
                <button
                  onClick={() => {
                    setMobileMenuOpen(false)
                    onLogout?.()
                  }}
                  className="px-4 py-3 rounded-lg font-medium text-destructive hover:bg-destructive/10 flex items-center gap-2 text-left"
                >
                  <LogOut className="h-4 w-4" />
                  Выйти
                </button>
              </>
            ) : (
              <>
                <Link
                  to="/login"
                  onClick={() => setMobileMenuOpen(false)}
                  className="px-4 py-3 rounded-lg font-medium text-foreground/80 hover:bg-background-light"
                >
                  Войти
                </Link>
                <Link
                  to="/register"
                  onClick={() => setMobileMenuOpen(false)}
                  className="px-4 py-3 rounded-lg font-medium bg-primary text-primary-foreground text-center"
                >
                  Регистрация
                </Link>
              </>
            )}
          </nav>
        </div>
      )}
    </header>
  )
}

export default Header


