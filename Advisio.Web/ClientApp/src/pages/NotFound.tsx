import { Link } from "react-router-dom";
import Layout from "@/components/Layout";
import { Button } from "@/components/ui/button";

const NotFound = () => {
  return (
    <Layout>
      <div className="container py-16 text-center">
        <h1 className="text-4xl md:text-6xl font-display font-bold mb-4 text-foreground">
          404
        </h1>
        <p className="text-xl text-muted-foreground mb-8">
          Страница не найдена
        </p>
        <Link to="/">
          <Button size="lg">Вернуться на главную</Button>
        </Link>
      </div>
    </Layout>
  );
};

export default NotFound;


