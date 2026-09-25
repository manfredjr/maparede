# Publicação

O MapNet - MT tem duas publicações, e elas não se misturam:

| O quê | Para onde | Como |
|---|---|---|
| O programa (`mapnet.exe`) | GitHub Releases | Marca de versão enviada ao GitHub. O CI testa, gera e publica |
| A página (pasta `public/`) | `mapnet.manfred.com.br`, no cPanel da GoDaddy, atrás do Cloudflare | Git Version Control do cPanel |

As duas só acontecem com autorização do Manfred. A página segue o roteiro padrão de publicação pelo Git do cPanel (documento interno do método), com as diferenças abaixo.

## 1. Como fica neste projeto

- O cPanel clona este próprio repositório em `~/repositories/mapnet`. Não há repositório de publicação: a página é estática, versionada e vem de um lugar só.
- A raiz do subdomínio é `repositories/mapnet/public`. O código C# fica fora da raiz e não é servido.
- O clone é por **HTTPS**, sem chave: o repositório é público. Nada em `~/.ssh` é tocado.
- Não há PHP, banco, `.env` nem tarefa agendada.
- O botão de download da página aponta para `releases/latest/download/mapnet.exe`. Antes da primeira Release, esse link responde 404.

Tudo que entra em `public/` fica público. O teste `SiteTestes` recusa o que não for arquivo de site ali dentro e confere que o link de download usa o nome do executável.

## 2. Publicar o programa

### Antes de publicar

1. O Pull Request da fatia foi juntado ao `main` com a frase de autorização.
2. O CI do `main` passou, conferido na tela do Actions.
3. A versão em `Directory.Build.props` (`<Version>`) foi aumentada no mesmo Pull Request. O CI recusa a marca que não bate com ela.
4. O `.exe` do CI (em **Actions**, execução do `main`, **Artifacts**) foi testado na rede da MT, pela janela e pela linha de comando.
5. A tabela OUI foi atualizada há menos de seis meses (`ferramentas\atualizar-oui.ps1`).

### Publicar

Na pasta do projeto, com o `main` atualizado:

```bat
git checkout main
git pull
git tag v0.1.0
git push origin v0.1.0
```

O CI roda os testes, gera o `mapnet.exe` e cria a Release `MapNet - MT v0.1.0` com o arquivo anexado. Acompanhar em **Actions**.

### Depois de publicar

1. Abrir `https://github.com/manfredjr/mapnet/releases/latest/download/mapnet.exe` e conferir que o download começa. A Release leva também o `mapnet.exe.sha256.txt`, para quem baixou conferir o arquivo.
2. Atualizar a "Situação do projeto" do README.
3. Anotar em `docs/superpowers/pendencias.md` o que o uso em campo mostrar.

## 3. Publicar a página: primeira vez

Com o Pull Request que traz o `.cpanel.yml` já juntado ao `main`.

**Ordem importa.** O clone vem antes do subdomínio. Se o subdomínio for criado primeiro, o cPanel cria a pasta `public` (com uma `cgi-bin` vazia) dentro de `repositories/mapnet`, e o Git Version Control recusa clonar em pasta que não está vazia. Foi o que aconteceu na primeira publicação, ainda com o nome MapaRede.

**1. Conferir a pasta** (Terminal do cPanel, colar o resultado):

```bash
if [ -e ~/repositories/mapnet ]; then find ~/repositories/mapnet -maxdepth 2 -ls; else echo "nao existe: ~/repositories/mapnet"; fi
```

Se aparecerem só pastas vazias, removê-las com `rmdir`, da mais funda para a de fora. O `rmdir` recusa pasta com arquivo. Se aparecer qualquer arquivo, parar e mandar o resultado.

**2. Criar o clone** em Git Version Control, botão Criar:

| Campo | Valor |
|---|---|
| Clone a Repository | ligado |
| Clone URL | `https://github.com/manfredjr/mapnet.git` |
| Repository Path | `repositories/mapnet` |
| Repository Name | `mapnet` |

**3. Abrir a pasta para o Apache** (colar o resultado, deve mostrar `755`):

```bash
chmod 755 ~/repositories/mapnet && stat -c '%a %n' ~/repositories/mapnet ~/repositories/mapnet/public/index.html
```

**4. Subdomínio.** No Cloudflare, criar o registro `A` com o nome `mapnet`, apontando para o IP da hospedagem e com proxy ligado. No cPanel, em Domínios, criar `mapnet.manfred.com.br` com **Compartilhar raiz com o domínio principal desligado** e a raiz `repositories/mapnet/public`.

**5. Certificado.** Em Status SSL/TLS, rodar o AutoSSL e conferir que `mapnet.manfred.com.br` ganhou certificado. Sem ele, o Cloudflare responde **erro 526**. Se o AutoSSL falhar para esse nome, deixar o registro `mapnet` como "DNS only" no Cloudflare, rodar de novo e voltar para o modo com proxy.

**6. Primeiro deploy.** Em Git Version Control, Gerenciar, aba Pull or Deploy: Update from Remote, F5, Deploy HEAD Commit. Acompanhar:

```bash
tail -40 "$(ls -t ~/.cpanel/logs/vc_*deploy*.log | head -1)"
```

**7. Conferir.** Purgar o cache do Cloudflare e abrir `https://mapnet.manfred.com.br/`. Conferir que a página abre com o logo da MT e que o botão de download baixa o executável do GitHub. Depois, ligar o Force HTTPS Redirect do subdomínio no cPanel.

## 4. Nome anterior

O programa se chamou MapaRede - MT até 25/09/2026. Como ainda não havia usuários, a publicação recomeçou do zero com o nome MapNet - MT: o endereço `maparede.manfred.com.br` foi desligado sem redirecionamento. O GitHub redireciona sozinho o endereço antigo do repositório para o novo.

## 5. Publicar a página: versão nova

Merge no `main`, depois no cPanel **Update from Remote**, **F5** e **Deploy HEAD Commit**. Com o Cloudflare na frente, purgar o cache antes de conferir a página.
