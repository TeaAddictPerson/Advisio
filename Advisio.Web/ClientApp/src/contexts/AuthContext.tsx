import { createContext, useContext, useState, useEffect, ReactNode } from "react";
import { toast } from "sonner";

interface User {
  id: number;
  username: string;
  email: string;
  role: string;
}

interface AuthContextType {
  user: User | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<boolean>;
  register: (username: string, email: string, password: string) => Promise<boolean>;
  logout: () => Promise<void>;
  checkAuth: () => Promise<void>;
  updateProfile: (username: string, email: string, newPassword?: string, currentPassword?: string) => Promise<boolean>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return context;
};

interface AuthProviderProps {
  children: ReactNode;
}

export const AuthProvider = ({ children }: AuthProviderProps) => {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const checkAuth = async () => {
    try {
      const response = await fetch("/api/Auth/check", {
        credentials: "include",
      });
      if (response.ok) {
        const data = await response.json();
        if (data.isAuthenticated) {
          // API возвращает роль как число (0 = User, 1 = Admin)
          let roleValue = data.role;
          if (typeof roleValue === "string") {
            // Если пришла строка, преобразуем в число
            if (roleValue === "Admin" || roleValue === "admin") {
              roleValue = 1;
            } else if (roleValue === "User" || roleValue === "user") {
              roleValue = 0;
            } else {
              roleValue = parseInt(roleValue) || 0;
            }
          }
          // Сохраняем как строку для совместимости с функцией isAdmin
          setUser({
            id: data.userId,
            username: data.username,
            email: data.email || "",
            role: roleValue.toString(),
          });
        } else {
          setUser(null);
        }
      }
    } catch (error) {
      console.error("Failed to check auth:", error);
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    checkAuth();
  }, []);

  const login = async (email: string, password: string): Promise<boolean> => {
    try {
      const response = await fetch("/api/Auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ email, password }),
      });

      if (response.ok) {
        const data = await response.json();
        if (data.success && data.user) {
          // Преобразуем роль в строку для совместимости
          let roleValue = data.user.role;
          if (typeof roleValue === "number") {
            roleValue = roleValue.toString();
          } else if (typeof roleValue === "string") {
            // Оставляем как есть, если строка
          } else {
            roleValue = "0";
          }
          setUser({
            id: data.user.id,
            username: data.user.username,
            email: data.user.email,
            role: roleValue,
          });
          toast.success(data.message || "Вход выполнен успешно");
          return true;
        }
      } else {
        const data = await response.json();
        toast.error(data.message || "Неверный email или пароль");
      }
    } catch (error) {
      console.error("Login error:", error);
      toast.error("Ошибка при входе");
    }
    return false;
  };

  const register = async (username: string, email: string, password: string): Promise<boolean> => {
    try {
      const response = await fetch("/api/Auth/register", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({ username, email, password }),
      });

      if (response.ok) {
        const data = await response.json();
        if (data.success && data.user) {
          // Преобразуем роль в число, если она enum
          let roleValue = data.user.role;
          if (typeof roleValue === "number") {
            roleValue = roleValue.toString();
          } else if (typeof roleValue === "string") {
            // Оставляем как есть, если строка
          } else {
            roleValue = "0";
          }
          setUser({
            id: data.user.id,
            username: data.user.username,
            email: data.user.email,
            role: roleValue,
          });
          toast.success(data.message || "Регистрация успешна");
          return true;
        }
      } else {
        const data = await response.json();
        toast.error(data.message || "Ошибка при регистрации");
      }
    } catch (error) {
      console.error("Register error:", error);
      toast.error("Ошибка при регистрации");
    }
    return false;
  };

  const logout = async () => {
    try {
      await fetch("/api/Auth/logout", {
        method: "POST",
        credentials: "include",
      });
      setUser(null);
      toast.success("Выход выполнен успешно");
    } catch (error) {
      console.error("Logout error:", error);
      toast.error("Ошибка при выходе");
    }
  };

  const updateProfile = async (
    username: string,
    email: string,
    newPassword?: string,
    currentPassword?: string
  ): Promise<boolean> => {
    try {
      const response = await fetch("/api/Auth/profile", {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        credentials: "include",
        body: JSON.stringify({
          username,
          email,
          newPassword,
          currentPassword,
        }),
      });

      if (response.ok) {
        const data = await response.json();
        if (data.success && data.user) {
          setUser({
            id: data.user.id,
            username: data.user.username,
            email: data.user.email,
            role: data.user.role,
          });
          toast.success(data.message || "Профиль обновлен");
          return true;
        }
      } else {
        const data = await response.json();
        toast.error(data.message || "Ошибка при обновлении профиля");
      }
    } catch (error) {
      console.error("Update profile error:", error);
      toast.error("Ошибка при обновлении профиля");
    }
    return false;
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user,
        isLoading,
        login,
        register,
        logout,
        checkAuth,
        updateProfile,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

