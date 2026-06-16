import Layout from "@/components/Layout";
import FeatureCard from "@/components/FeatureCard";
import { Card } from "@/components/ui/card";
import { MapPin, Sparkles, ClipboardList, Info } from "lucide-react";

const Index = () => {
  return (
    <Layout>
      {/* Hero Section */}
      <section className="py-16 md:py-24">
        <div className="container">
          <div className="text-center max-w-3xl mx-auto animate-fade-in">
            <h1 className="text-4xl md:text-5xl lg:text-6xl font-display font-bold text-foreground mb-6 leading-tight">
              <span className="text-primary">Advisio</span> - система рекомендаций мест отдыха
            </h1>
            <p className="text-lg md:text-xl text-muted-foreground font-body leading-relaxed">
              Найдите идеальное место для отдыха с друзьями, семьей или детьми. 
              Персонализированные рекомендации на основе ваших предпочтений.
            </p>
          </div>
        </div>
      </section>

      {/* Feature Cards */}
      <section className="pb-16 md:pb-24">
        <div className="container">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6 lg:gap-8">
            <FeatureCard
              icon={MapPin}
              title="Места отдыха"
              description="Просмотрите все доступные места для отдыха в вашем городе"
              buttonText="Перейти к местам"
              href="/places"
              variant="primary"
              delay="0.1s"
            />
            <FeatureCard
              icon={Sparkles}
              title="Рекомендации"
              description="Получите персональные рекомендации на основе ваших предпочтений"
              buttonText="Мои рекомендации"
              href="/recommendations"
              variant="success"
              delay="0.2s"
            />
            <FeatureCard
              icon={ClipboardList}
              title="Опрос"
              description="Пройдите опрос для улучшения качества рекомендаций"
              buttonText="Пройти опрос"
              href="/survey"
              variant="info"
              delay="0.3s"
            />
          </div>
        </div>
      </section>

      {/* Info Section */}
      <section className="pb-16 md:pb-24">
        <div className="container">
          <div 
            className="bg-card/50 backdrop-blur-sm border border-border/50 rounded-2xl p-8 animate-slide-up opacity-0"
            style={{ animationDelay: "0.4s", animationFillMode: "forwards" }}
          >
            <div className="flex items-start gap-4">
              <div className="w-12 h-12 rounded-xl bg-secondary/10 flex items-center justify-center flex-shrink-0">
                <Info className="h-6 w-6 text-secondary" />
              </div>
              <div>
                <h2 className="text-xl font-display font-semibold mb-4 text-foreground">
                  Как это работает?
                </h2>
                <ul className="space-y-3 text-muted-foreground">
                  <li className="flex items-start gap-2">
                    <span className="w-1.5 h-1.5 rounded-full bg-primary mt-2 flex-shrink-0" />
                    <span>Для новых пользователей система предлагает несколько мест из каждой категории</span>
                  </li>
                  <li className="flex items-start gap-2">
                    <span className="w-1.5 h-1.5 rounded-full bg-primary mt-2 flex-shrink-0" />
                    <span>После прохождения опроса рекомендации становятся более точными</span>
                  </li>
                  <li className="flex items-start gap-2">
                    <span className="w-1.5 h-1.5 rounded-full bg-primary mt-2 flex-shrink-0" />
                    <span>Система учитывает ваши предпочтения и исключает уже просмотренные места</span>
                  </li>
                </ul>
              </div>
            </div>
          </div>
        </div>
      </section>
    </Layout>
  );
};

export default Index;


