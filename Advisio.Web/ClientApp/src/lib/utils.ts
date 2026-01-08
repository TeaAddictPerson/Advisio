import { type ClassValue, clsx } from "clsx"
import { twMerge } from "tailwind-merge"

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function isAdmin(role?: string | number): boolean {
  if (role === undefined || role === null) return false;
  // Проверяем как число
  if (typeof role === "number") {
    return role === 1;
  }
  // Проверяем как строку
  if (typeof role === "string") {
    // Проверяем числовую строку
    const numRole = parseInt(role);
    if (!isNaN(numRole)) {
      return numRole === 1;
    }
    // Проверяем текстовые значения
    return role === "1" || role === "Admin" || role === "admin" || role.toLowerCase() === "admin";
  }
  return false;
}


