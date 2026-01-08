import * as React from "react"
import { Slot } from "@radix-ui/react-slot"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-medium font-body transition-all duration-300 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 [&_svg]:pointer-events-none [&_svg]:size-4 [&_svg]:shrink-0",
  {
    variants: {
      variant: {
        default:
          "bg-primary text-primary-foreground shadow-soft hover:bg-primary/90 hover:shadow-card active:scale-[0.98]",
        destructive:
          "bg-destructive text-destructive-foreground shadow-soft hover:bg-destructive/90 hover:shadow-card",
        outline:
          "border-2 border-primary bg-transparent text-primary hover:bg-primary hover:text-primary-foreground",
        secondary:
          "bg-secondary text-secondary-foreground shadow-soft hover:bg-secondary/80 hover:shadow-card",
        ghost: 
          "text-foreground hover:bg-muted/50 hover:text-foreground",
        link: 
          "text-primary underline-offset-4 hover:underline",
        success:
          "bg-success text-success-foreground shadow-soft hover:bg-success/90 hover:shadow-card",
        warning:
          "bg-warning text-warning-foreground shadow-soft hover:bg-warning/90 hover:shadow-card",
        hero:
          "bg-primary text-primary-foreground shadow-elevated hover:shadow-card hover:scale-105 active:scale-[0.98] btn-glow font-semibold",
        favorite:
          "bg-warning text-warning-foreground hover:bg-warning/90 shadow-soft",
        "favorite-outline":
          "border-2 border-warning text-warning bg-transparent hover:bg-warning hover:text-warning-foreground",
        like:
          "bg-destructive text-destructive-foreground hover:bg-destructive/90 shadow-soft",
        "like-outline":
          "border-2 border-destructive text-destructive bg-transparent hover:bg-destructive hover:text-destructive-foreground",
      },
      size: {
        default: "h-10 px-5 py-2",
        sm: "h-9 rounded-md px-3 text-xs",
        lg: "h-12 rounded-lg px-8 text-base",
        xl: "h-14 rounded-xl px-10 text-lg",
        icon: "h-10 w-10",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : "button"
    return <Comp className={cn(buttonVariants({ variant, size, className }))} ref={ref} {...props} />
  },
)
Button.displayName = "Button"

export { Button, buttonVariants }


