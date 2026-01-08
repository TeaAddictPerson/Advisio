import { ReactNode } from "react";
import Header from "./Header";
import Footer from "./Footer";
import { useAuth } from "@/contexts/AuthContext";
import { isAdmin } from "@/lib/utils";

interface LayoutProps {
  children: ReactNode;
}

const Layout = ({ children }: LayoutProps) => {
  const { user, isAuthenticated, logout } = useAuth();

  return (
    <div className="min-h-screen flex flex-col">
      <Header 
        isAuthenticated={isAuthenticated}
        username={user?.username}
        isAdmin={isAdmin(user?.role)}
        onLogout={logout}
      />
      <main className="flex-1">
        {children}
      </main>
      <Footer />
    </div>
  );
};

export default Layout;
