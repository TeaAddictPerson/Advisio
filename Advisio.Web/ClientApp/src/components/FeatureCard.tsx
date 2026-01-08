import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Link } from "react-router-dom";
import { LucideIcon } from "lucide-react";

interface FeatureCardProps {
  title: string;
  description: string;
  buttonText: string;
  href: string;
  icon: LucideIcon;
  variant?: "primary" | "success" | "info";
  delay?: string;
}

const variantStyles = {
  primary: "hover:border-primary/50",
  success: "hover:border-success/50",
  info: "hover:border-secondary/50",
};

const buttonVariants = {
  primary: "default" as const,
  success: "success" as const,
  info: "secondary" as const,
};

const FeatureCard = ({
  title,
  description,
  buttonText,
  href,
  icon: Icon,
  variant = "primary",
  delay = "0s",
}: FeatureCardProps) => {
  return (
    <Card 
      variant="interactive" 
      className={`${variantStyles[variant]} animate-slide-up opacity-0`}
      style={{ animationDelay: delay, animationFillMode: "forwards" }}
    >
      <CardHeader className="text-center pb-4">
        <div className="mx-auto mb-4 w-14 h-14 rounded-2xl bg-primary/10 flex items-center justify-center">
          <Icon className="h-7 w-7 text-primary" />
        </div>
        <CardTitle className="text-xl">{title}</CardTitle>
        <CardDescription className="text-base mt-2">
          {description}
        </CardDescription>
      </CardHeader>
      <CardContent className="text-center">
        <Link to={href}>
          <Button variant={buttonVariants[variant]} size="lg" className="w-full">
            {buttonText}
          </Button>
        </Link>
      </CardContent>
    </Card>
  );
};

export default FeatureCard;


