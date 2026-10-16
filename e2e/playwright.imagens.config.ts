import { defineConfig } from '@playwright/test';
import padrao from './playwright.config';

/*
 * Os mesmos testes contra a pilha do Compose: as imagens que vão para
 * produção, com os papéis de produção no banco (ADR-0020). Quem sobe a pilha é
 * o Compose, e E2E_WEB diz onde ela está; aqui, só o navegador.
 */
export default defineConfig({ ...padrao, webServer: [] });
