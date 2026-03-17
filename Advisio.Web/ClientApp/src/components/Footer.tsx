import { Link } from "react-router-dom"

const Footer = () => {
  return (
    <footer className="border-t border-border/50 bg-background-dark/30">
      <div className="container py-6 flex flex-col sm:flex-row items-center justify-between gap-4">
        <p className="text-sm text-muted-foreground font-body">
          © 2025 - Advisio.Web
        </p>
        <Link 
          to="/privacy" 
          className="text-sm text-muted-foreground hover:text-foreground transition-colors"
        >
          Privacy
        </Link>
      </div>
    </footer>
  )
}

export default Footer





