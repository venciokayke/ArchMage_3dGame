# Underdog 3D — sobrevivencia

## Aplicar no Editor

1. Use Unity **2022.3.62f3**. Aguarde a resolucao dos pacotes e a compilacao.
2. Abra **somente a ArenaTeste aprovada**, com sua camera atual, Environment e SpawnPoints. Salve manualmente as alteracoes da arena.
3. Execute **Tools > Underdog > Prepare Survival Game** e confirme a preparacao.
4. O comando cria **Assets/Scenes/UnderdogSurvival.unity** e abre essa cena. A arena original e fechada, sem ser sobrescrita, para nao duplicar jogadores ou cameras.
5. Entre em Play Mode e clique **Iniciar** (ou Enter). LMB: projetil, RMB: explosao, Q: rajada, Shift: dash, WASD: mover.
6. Ao perder ou vencer, use **Jogar novamente** (ou R). A cena e recarregada com HP, cooldowns, inimigos e contadores novos.

O preparador NAO altera angulo, tamanho, Follow, prioridades, Body, Aim ou Brain da camera aprovada. Nao execute Generate Test Arena para esta etapa: esse outro comando reconstroi o mapa. Prepare Survival Game aproveita o mapa salvo.

Se UnderdogSurvival ja existir, ha confirmacao separada e backup antes de substituir. Cada execucao cria uma pasta unica em Assets/Generated/UnderdogSurvival*. Ela contem os assets novos, uma copia da origem, a copia anterior da partida quando aplicavel e um backup dos Build Settings. A nova cena fica no inicio dos Build Settings para iniciar a sobrevivencia ao executar um build; as outras entradas e seus estados sao mantidos, e o reinicio recarrega a propria partida. Nunca rode o preparador usando uma partida ja preparada como origem.

Uma falha preserva a origem e fecha a copia de trabalho. Assets intermediarios podem permanecer na pasta Generated para inspecao. Nao sao editados importadores KayKit, prefabs originais nem YAML de cenas/prefabs.

## O que foi integrado

- Vida e a fonte unica de HP. PlayerHp conserva ReceberDano(int), Curar(int) e AumentarVidaMaxima(int), mantendo compatibilidade com loja e armadilhas.
- Jogador: 100 HP e 0.6 s de invulnerabilidade apos dano, configuravel em PlayerHp. Hit produz particulas e flash; a animacao usa uma camada superior independente. Morte bloqueia comandos e colliders, executa Death uma vez e conserva o corpo visivel para a animacao.
- Habilidades sincronizadas exclusivamente por **LancarMagia**. Pendencias, cooldowns e sequencias sao cancelados ao desativar/morrer. Eventos repetidos nao repetem o ataque. Mana nao foi introduzida: as habilidades usam cooldowns.
- Projeteis varrem o trecho percorrido por SphereCast e verificam sobreposicao inicial. Respeitam o dono, ignoram aliados e deixam de causar dano apos a morte do conjurador.
- Explosao aplica dano uma vez por Vida, mesmo com varios colliders, e nao atravessa paredes.
- EnemyAI continua sendo a implementacao de inimigo. Os flags usarNavMesh e usarAnimationEvent ficam ativos apenas nos prefabs novos; as configuracoes antigas mantem o caminho legado. Os prefabs novos aplicam dano apenas pelo evento AplicarGolpe; o timer antigo nao duplica o golpe.
- NavMeshAgent calcula caminho/desvio, mas **updatePosition e updateRotation sao desligados em runtime**. O CharacterController aplica o deslocamento e suas colisoes. O inimigo distante recua quando o jogador se aproxima e busca linha de tiro quando um obstaculo bloqueia a visao.
- Mortos nao atacam nem bloqueiam o jogador. Os inimigos sao removidos apos a duracao de Death mais uma pequena margem.
- Inicio, HUD, contador de onda/inimigos, cooldowns, Game Over, vitoria e reinicio por recarga da cena. O tempo continua avancando no fim para permitir as animacoes de morte.

## Balanceamento inicial

| Habilidade | Entrada | Dano | Cooldown | Outros valores |
|---|---|---:|---:|---|
| Projetil | LMB | 25 | 0.5 s | Velocidade 18, vida util 3 s |
| Explosao | RMB | 35 por inimigo | 4 s | Raio 4.5 |
| Rajada | Q | 15 por projetil | 6 s | 3 projeteis, intervalo 0.1 s, velocidade 22 |

O preparo da primaria usa o evento existente em 0.32457352 s do clipe, com estado em velocidade 2: emissao apos aproximadamente 0.162 s. Summon tem 4.3 s no pacote: a explosao sai em 35% do clipe, cerca de 0.75 s com velocidade 2. A rajada usa Spellcasting, tambem com estado em velocidade 2. Depois da emissao, outro ataque pode substituir a recuperacao visual, respeitando seu cooldown; antes do evento a pendencia nao e descartada por outro clique.

| Arquetipo | Modelo | HP | Dano | Velocidade | Comportamento |
|---|---|---:|---:|---:|---|
| Basico | Skeleton_Warrior | 100 | 10 | 2.5 | Melee, alcance 1.5, intervalo 1.5 s |
| Rapido | Skeleton_Rogue | 60 | 8 | 4 | Melee, alcance 1.5, intervalo 1.2 s |
| Distante | Skeleton_Mage | 75 | 12 | 2.6 | Alcance 8, recuo abaixo de 4, intervalo 2 s |

O basico e o rapido reutilizam a referencia de arma do inimigo existente na arena. O antigo inimigo da cena e preservado, mas fica inativo na copia para nao disputar a partida com os spawns do WaveManager.

| Onda | Basicos | Rapidos | Distantes |
|---:|---:|---:|---:|
| 1 | 4 | 0 | 0 |
| 2 | 4 | 2 | 0 |
| 3 | 4 | 2 | 2 |
| 4 | 4 | 4 | 2 |
| 5 | 5 | 4 | 3 |

Intervalo entre ondas: 4 s. Espacamento de spawn: 0.7 s. Distancia minima do jogador: 6. Se um ponto estiver ocupado, sem caminho ou muito proximo do jogador, o manager tenta outro; se nenhum for seguro, aguarda em vez de forcar o spawn.

## Animacoes reais e Controllers

Origem pesquisada pelo AssetDatabase: **Assets/Assets_Externos/KayKit_Character_Animations_1.1/Animations/fbx/Rig_Medium**. O preparador exige os clipes pelos nomes reais, copia-os para assets .anim proprios e limpa/redefine eventos somente nas copias.

Clipes usados:

- Idle_A e Running_A.
- Walking_Backwards, Running_Strafe_Left e Running_Strafe_Right.
- Dodge_Forward, Dodge_Backward, Dodge_Left e Dodge_Right.
- Hit_A e Death_A.
- Ranged_Magic_Raise, Ranged_Magic_Summon e Ranged_Magic_Spellcasting para o Mago.
- Melee_1H_Attack_Chop para melee e rapido; Ranged_Magic_Shoot para o distante.

Modelos existentes resolvidos pelo AssetDatabase na pasta KayKit_Skeletons_1.1_FREE: Skeleton_Warrior, Skeleton_Rogue e Skeleton_Mage. O modelo Mage ja presente na cena e preservado, incluindo cajado e SpellSpawnPoint.

O Controller do jogador possui:

- Base Layer: Blend Tree direcional de locomocao, Blend Tree direcional de Dash e Death.
- Upper Body: primaria, explosao e rajada, com mascara de Transform baseada nos ossos reais abaixo de spine. Permite locomocao durante ataques. O peso volta a zero quando o ataque termina para nao prender os bracos na ultima pose.
- Feedback: Hit superior independente, sem cancelar os eventos de ataque. Death desliga as camadas superiores.

A duracao dos clipes Dodge e ajustada a duracao de dash do PlayerMovement. Root motion fica desativado. Os inimigos recebem Controller com locomocao por Speed e estados Attack/Hit/Death. Hit nao interrompe um golpe pendente; o flash visual continua ocorrendo. Death nao possui transicao de saida ou loop.

Os eventos novos sao definidos nas copias dos clipes. Nao e necessario adicionar eventos manualmente aos FBXs. A calibracao visual do instante da explosao pode ser refinada nas copias geradas, sem alterar o pacote original.

## Efeitos e materiais

O preparador cria materiais URP/Particles/Unlit com textura radial gerada localmente, ParticleSystems e TrailRenderers. Nao ha downloads ou pacotes externos novos.

- Primaria azul: preparo na ponta do cajado, nucleo de particulas, rastro e impacto.
- Explosao violeta: preparo proprio e anel de area.
- Rajada violeta: preparo, projeteis e impactos distintos da primaria.
- Inimigo distante: projetil e impacto ambar.
- Flash e particulas de Hit; particulas de morte; pulso de dash.

Os efeitos temporarios se removem automaticamente. O preparador cria materiais novos para personagens quando encontra shaders antigos; texturas e materiais originais nao sao sobrescritos. HDR/Bloom do Volume aprovado nao sao alterados: a visibilidade dos efeitos precisa ser conferida com as configuracoes reais da arena.

## Navegacao, colisoes e seguranca

O preparador usa NavMeshBuilder nativo da Unity 2022.3, coleta Physics Colliders somente de Environment e marca Walls/Props como Not Walkable. O NavMeshData e salvo como asset e carregado por ArenaNavigation. Nao e necessario instalar AI Navigation ou adicionar outro NavMeshSurface.

O bake usa altura 2, raio 0.45, step 0.3, inclinacao 45 e voxel 0.1. Os agentes tem raio 0.4 e altura 2. A preparacao exige caminhos completos de todos os EnemySpawn_ ao jogador antes de salvar.

As paredes visuais baixas e o enquadramento da camera sao mantidos. Somente na copia de sobrevivencia, seus BoxColliders de limite sobem ate pelo menos 3 m para segurar magias que passam acima da geometria baixa. Props usam os colliders ja existentes. Nao foram adicionadas primitivas visuais para substituir KayKit.

Para reiniciar corretamente, mantenha UnderdogSurvival habilitada nos Build Settings (o preparador faz isso). Abra apenas uma cena para testar; cenas aditivas podem duplicar personagens, cameras e dados de navegacao.

## Arquivos

Modificados:
- Assets/Projectile.cs
- Assets/Scripts/Combat/Vida.cs
- Assets/Scripts/Player/PlayerHp.cs
- Assets/Scripts/Player/PlayerCombat.cs
- Assets/Scripts/Player/PlayerMovement.cs
- Assets/Scripts/Enemy/EnemyAI.cs
- Packages/manifest.json e Packages/packages-lock.json

Criados, com seus .meta:
- Assets/Scripts/Combat/CombatFeedback.cs
- Assets/Scripts/Combat/TimedEffect.cs
- Assets/Scripts/Game/ArenaNavigation.cs
- Assets/Scripts/Game/WaveManager.cs
- Assets/Scripts/Game/SurvivalHUD.cs
- Assets/Editor/SurvivalGameSetup.cs
- Assets/Tests/Editor/SurvivalChecks.cs

A camera, os Controllers e prefabs originais e as cenas existentes nao foram sobrescritos. Os pacotes URP/uGUI foram alinhados a **14.0.12 / 1.0.0**, presentes no cache e compativeis com Unity 2022.3, em vez das referencias anteriores a 17.5.0 / 2.5.0. O Input System 1.19 foi mantido (declara compatibilidade com 2022.3); a entrada legada continua funcionando com Both. ProjectVersion.txt permanece 2022.3.62f3.

Backup inicial de scripts, configuracao e arena: Temp/SurvivalBackup-20261114-210100. As copias estruturais persistentes da cena e Build Settings sao criadas pelo preparador na pasta Generated. Nao houve commit, push ou merge nesta etapa.

## Configuracao pelo Inspector

Apos preparar, nao e necessario conectar referencias manualmente. Para balancear:

- Mage / PlayerCombat: dano e velocidade primarios, cooldown, zona morta, Explosao e Rajada.
- Mage / PlayerHp: invulnerabilidade; HP maximo em vidaMaxima (o preparador configura 100).
- SurvivalGame / WaveManager: composicao das ondas, intervalos, distancia segura e prefabs.
- Prefabs Melee/Fast/Ranged gerados: Vida, EnemyAI, NavMeshAgent, referencias de efeitos.
- Assets .anim e Controllers gerados: eventos e velocidades; as copias KayKit sao independentes dos originais.
- Prefabs de efeitos e materiais gerados: tamanho, duracao, intensidade e cor.

Reexecutar o preparador cria uma nova configuracao padrao. Alteracoes artesanais na partida anterior ficam no backup, nao sao mescladas automaticamente.

## Validacao e testes

**Compilacao:** verificada por fase contra as bibliotecas da Unity 2022.3.62f3, incluindo Editor e uGUI. Permanecem os avisos anteriores de NavigationStatic obsoleto no TestArenaGenerator e ItemEffect.tipoEfeito sem atribuicao em codigo.

**Automacao real:** executada em projeto isolado em Temp/SurvivalPlayVerification, usando Unity 2022.3 e copias dos modelos/clipes reais. Os relatorios XML sao preservados em Docs/Validation.

- Edit Mode: HP, invulnerabilidade, cura, morte unica inclusive com dano encadeado por evento; regressao do combate existente; Controllers, mascara superior com caminhos de ossos reais, efeitos, tres prefabs, NavMesh e HUD; camera de teste inalterada.
- Play Mode: SphereCast com passos de 1/60 e 1/10 s, dano unico, bloqueio apos morte do conjurador, area sem duplicar dano e sem atravessar parede; cinco ondas vazias para verificar estados; cena isolada com tres arquetipos reais, tres habilidades disparadas pelos eventos, primaria durante Hit+dash, recuo do distante, vitoria, reinicio limpo e Death/Game Over.
- Os testes de ondas vazias verificam transicoes, nao balanceamento. O teste com tres inimigos usa uma onda controlada, nao uma partida completa de cinco ondas.

Checagens disponiveis no projeto:
1. Tools > Underdog > Check Combat Event (Edit Mode).
2. Tools > Underdog > Check Survival (Edit Mode).
3. Em Play Mode sem pausa: Tools > Underdog > Check Survival (Play Mode). Esta checagem cria objetos temporarios isolados longe da arena, testa funcoes e os remove; nao substitui jogar a partida.

**Teste manual necessario na arena aprovada:**

1. Prepare e confira que a Game View preserva a camera aprovada e que ha somente um jogador/camera ativos.
2. Inicie; teste WASD, movimento lateral/recuo e dash em todas as direcoes, inclusive contra paredes, cantos e props.
3. Teste cada habilidade parado, andando e durante dash. Confira preparo, direcao do cajado, projeteis, impactos e cooldowns. Cliques durante cooldown ou preparo sao rejeitados; cada solicitacao aceita deve executar um evento ou ter cancelamento intencional.
4. Verifique que Hit nao elimina o disparo aceito e que a morte cancela ataques/rajadas, bloqueia comandos e toca Death uma vez.
5. Teste dano/colisoes em inimigos e paredes em diferentes FPS. Teste explosao contra varios inimigos e junto a uma parede.
6. Circule a arena; confira perseguicao, desvio de props, recuo do distante e ausencia de inimigos presos. Navegacao foi testada em geometria isolada; a distribuicao exata dos obstaculos da arena precisa dessa conferencia.
7. Morra e reinicie; confirme HP 100, onda zero no inicio e nenhum inimigo/projetil da partida anterior.
8. Jogue as cinco ondas ate a vitoria. Ajuste quantidades, intervalos e velocidade no Inspector se a dificuldade nao estiver adequada.
9. Confira legibilidade dos efeitos, UI, sombras e animacoes na Game View. Nao houve avaliacao visual interativa, captura de tela ou calibracao final do balanceamento.

Sem mana, inventario ou arvore de habilidades nesta versao. A materializacao dos assets e da nova cena neste checkout exige executar Prepare Survival Game no Editor aberto; a arena original nao foi substituida automaticamente.

Referencias usadas: Ponytail; craft-game-floors (camera, lighting, VFX e game shell); UI/uGUI; navegacao. APIs verificadas na documentacao Unity 2022.3: NavMeshBuilder.BuildNavMeshData, Animator/AnimationUtility e SceneManager; pacotes conferidos pelos package.json locais. Os testes de Play Mode usam o runner oficial da Unity.
