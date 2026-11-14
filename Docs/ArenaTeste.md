# Arena de teste — Underdog 3D

## Estado e pre-requisitos

O checkout esta em main, no merge f08a4be do PR #6. A integracao inclui o dono do projetil, dano e inimigo da main e a mira XZ, dash e emissao por Animation Event da mage.

ATENCAO: ProjectVersion.txt continua 2022.3.62f3, mas Packages/manifest.json solicita URP 17.5.0 e uGUI 2.5.0. O cache local ainda contem URP 14.0.12 e uGUI 1.0.0. Isso nao valida o manifesto. Antes de gerar, resolva os pacotes para Unity 2022.3 pelo Package Manager (URP 14.0.12 e uGUI 1.0.0, configuracao anterior do projeto). Deixe a Unity resolver packages-lock.json. Nao abra na Unity 6 nem migre assets para contornar isso. O gerador nao modifica pacotes. Active Input Handling esta em Both, preservando Input.GetAxisRaw/mouse.

## Executar

1. Abra apenas Assets/Scenes/ShopAndHpTest.unity na Unity 2022.3.62f3. Salve manualmente alteracoes que queira manter. A cena deve conter exatamente um PlayerMovement, uma Camera, uma CinemachineVirtualCamera e ao menos um EnemyAI.
2. Aguarde a compilacao e confirme as referencias de PlayerCombat e o evento LancarMagia.
3. Execute Tools > Underdog > Generate Test Arena.
4. O gerador valida assets e referencias, salva uma copia temporaria da origem, abre essa copia e monta Assets/Scenes/ArenaTeste.unity. A origem nao e sobrescrita. Apos sucesso ela e fechada para evitar personagens duplicados no Play Mode.
5. Se ArenaTeste ja existir, e necessaria confirmacao no dialogo; antes de substitui-la e criado ArenaTeste_backup*.unity com caminho unico. Feche uma ArenaTeste aberta antes de executar. Cancelar nao altera a arena existente.
6. Uma falha fecha a copia e reativa a origem. Uma cena parcial nao e promovida ao destino antes das verificacoes. Um material gerado pode permanecer para reutilizacao. Nao e feita alteracao nos Build Settings: abra ArenaTeste diretamente para testar.

## Conteudo gerado

- Environment / Floor: grade 10x10 de modulos de 4 unidades; superficie Y=0, area X/Z -20 a +20. Variacao deterministica com pedras e rotacao.
- Environment / Walls: 40 segmentos. Frente (Z negativo) com 0,9 m; fundo com 1,4 m; laterais com 1,2 m. Altura baixa reduz obstrucao sem scripts de transparencia.
- Environment / Props: quatro colunas e quatro barris nas bordas. Miolo aberto.
- SpawnPoints: PlayerSpawn em (0, 0.15, -4), quatro EnemySpawn nos eixos. Marcadores sao Transforms: nao existe sistema de ondas ou respawn.
- Mantem todos os inimigos da origem; posiciona o primeiro a 8 unidades do jogador, dentro da deteccao atual de 10 m e do alcance nominal de 30 m do projetil.
- Objetos antigos de loja, armadilhas e piso sao mantidos, mas raizes sem jogador/inimigo/camera/luz/Volume ficam inativas APENAS na copia. Nada e apagado da origem. Confira a Hierarchy se a origem tiver uma organizacao diferente da cena recomendada.

## Assets reais

Pasta: Assets/Assets_Externos/KayKit_Dungeon_Pack_1.1_FREE/Assets/fbx(unity)

- floor_tile_large.fbx
- floor_tile_large_rocks.fbx
- wall_half.fbx
- column.fbx
- barrel_large.fbx

Resolucao via AssetDatabase.AssetPathToGUID / GUIDToAssetPath; instancias via PrefabUtility.InstantiatePrefab. Os limites dos Renderers sao medidos no Editor; escala e pivot dos modulos sao ajustados nas instancias, sem reimportar os FBXs.

Material criado e reutilizado em Assets/Generated/UnderdogArena/DungeonArena.mat: URP/Lit, textura original Assets/Assets_Externos/KayKit_Dungeon_Pack_1.1_FREE/Assets/textures/dungeon_texture.png. Nenhum material KayKit original e convertido ou sobrescrito. As dimensoes especificadas podem alterar as proporcoes originais; confira a aparencia no Editor.

## Camera e luz

Somente a camera da copia muda: ortografica, X=50 graus, Y=0, size=9, Follow no jogador, LookAt vazio, FramingTransposer com damping 0.6 e dead zone 0.05. Yaw zero preserva a relacao visual com WASD mundial; experimente yaw 30-45 manualmente se preferir diagonal isometrica, sabendo que W continua +Z. Distancia 25 afasta a camera das paredes; nao muda o tamanho aparente ortografico. Nao foi adicionado CinemachineCollider, que pode provocar reposicionamentos indesejados em interiores.

A arena usa paredes dianteiras baixas e decoracao lateral para reduzir oclusao. Isso nao garante visibilidade perfeita em todo canto ou proporcao de tela: teste os quatro limites, dash, 16:9 e 4:3 antes de aumentar as paredes. Size 9 nao mostra a arena inteira de 40 m; a camera acompanha o jogador.

Luz direcional quente com sombras suaves, intensidade 1.2 e ambiente frio em gradiente. O Volume existente e preservado. Confira no asset URP o shadow distance (sugestao 50-60 m), sombras, HDR e renderer escolhido; o gerador nao edita assets globais de renderizacao.

## Colisoes e NavMesh

Cada modulo tem um BoxCollider solido ajustado ao volume de destino. O piso e continuo; paredes e props bloqueiam CharacterController e SphereCast do projetil. Nao sao primitivas visuais substituindo os modelos.

Preparacao: Environment e seus filhos recebem NavigationStatic para o fluxo legado da Unity 2022.3. Esse enum e obsoleto, mas ainda compila nessa versao. Nenhum NavMesh e assado ou salvo pelo gerador.

Para o fluxo AI Navigation, adicione pelo Package Manager a versao oferecida como compativel com Unity 2022.3; adicione NavMeshSurface em Environment, Collect Objects = Children e Use Geometry = Physics Colliders. Assim os colliders antigos inativos e atores fora do Environment nao entram na coleta. Use raio aproximadamente 0.4, altura 2, step 0.3 e incl. maxima 45 como ponto inicial; bake e confira a cobertura azul e exclusao dos obstaculos. Nao marque props como caminhaveis no topo. Se usar o fluxo legado, confira as mesmas configuracoes na janela Navigation.

IMPORTANTE: EnemyAI ainda usa CharacterController e perseguicao direta, nao NavMeshAgent. Ele pode ficar preso em props. Nao adicione um Agent ativo sem integrar a movimentacao: dois controladores disputariam a posicao. Navegacao completa fica para uma proxima etapa.

## Combate revisado

PlayerCombat foi preservado: mira compartilhada XZ e zona morta, offset visual, cooldown 0.5, ataque iniciado por Animator.Play e instanciacao apenas em LancarMagia; dono atribuido antes de Projectile.Update. Evento no clipe a 0.32457352 s, velocidade do estado 2 (~0.162 s de reproducao). Transicao de saida e Controller existentes continuam intactos. Logs temporarios existentes permanecem disponiveis.

Correcao confirmada em EnemyAI: cancelar golpe pendente quando o alvo foi destruido, desativado ou morreu. Antes disso, o inimigo podia acessar um Transform nulo ou tentar atingir o jogador ja desativado. A checagem de Editor agora cobre alvo nulo e inativo.

## Verificacao manual

- Execute Tools > Underdog > Check Combat Event (Edit Mode).
- Gere a arena, confirme uma Camera, um jogador ativo e os inimigos originais. O gerador verifica contagens, referencias do combate, evento, colliders e piso de 40x40 antes de salvar.
- Teste WASD, diagonais, dash em todas as bordas e movimento durante o ataque. Nao deve cair entre tiles ou atravessar paredes.
- Passe o cursor junto ao personagem: cajado horizontal com offset e direcao estavel na zona morta.
- Clique em cadencia e rapidamente: cada ACEITO deve ter um INSTANCIADO ou cancelamento explicado. Rejeicoes por cooldown nao sao falhas.
- Acerte o esqueleto; verifique perseguicao, dano, morte e ignorar o proprio conjurador. Projeteis devem parar em paredes/barris/colunas.
- Deixe o inimigo iniciar um golpe e desative o jogador antes do impacto: sem erro ou dano posterior. Teste tambem morte normal.
- Reexecute o gerador: Cancelar deve preservar ArenaTeste; confirmar deve criar backup sem duplicar a montagem.
- A compilacao externa passou contra Unity 2022.3/Cinemachine 2.10; nao houve execucao do Editor, bake, captura visual ou teste de Play Mode. Avaliacao visual ainda pendente; nao ha pontuacao visual baseada apenas em compilacao.

Referencias consultadas: craft-game-floors (art-direction, camera-rigs, scene-lighting, visual-scorecard), Unity 2022.3 EditorSceneManager.SaveScene e Lighting, Cinemachine 2.10 Framing Transposer e requisitos do URP 14.


## Segunda iteracao: apresentacao e verificacao da Game View

### Diagnostico da primeira arena salva

Inspecao de ArenaTeste.unity: Main Camera e Camera_Principal possuem quaternion (X=0.4226183, W=0.9063078), equivalente a 50 graus. Hints de Euler estavam desatualizados (0 e 60); hints nao sao a orientacao efetiva. Main Camera tem Brain ativo (SmartUpdate), camera virtual com prioridade 10, Follow no Mage e LookAt vazio. Body e FramingTransposer e nao existe componente Aim. O arquivo nao confirma camera a 90 graus: e necessario medir a saida em Play Mode para identificar divergencia na Game View. Nenhuma cena foi editada diretamente nesta iteracao.

A camera gerada passa a configurar explicitamente Brain ativo em LateUpdate, blend de 0.25 s, virtual com prioridade 100 e sem parent, Follow no jogador, LookAt vazio, Aim = Do Nothing. Somente a camera real recebe a tag MainCamera. Rotacao X=50, Y=0; lente ortografica size 8.5, Dutch 0; damping 0.6, dead zone 0.05, jogador centralizado. Nenhum script de camera novo acompanha a rotacao do personagem. A geracao avalia a orientacao final do pipeline com jogador girado em 137 graus, antes de salvar.

### Aplicar

Abra apenas ShopAndHpTest, salve suas alteracoes e execute Generate Test Arena. Confirme a substituicao somente se quiser reconstruir ArenaTeste; a versao anterior recebe backup automaticamente. A arena aberta com alteracoes nao salvas nao e substituida. Assets, prefabs e cenas originais continuam preservados. Nao rode o gerador a partir de ArenaTeste: ele usa a origem para evitar acumulo de ambientes gerados.

As regras de copia/backup da primeira iteracao continuam iguais. Nao e preciso modificar manualmente o prefab da camera. Ajustes manuais feitos na arena anterior podem ser consultados no backup; a geracao nao tenta mesclar edicoes artesanais do mapa.

### Layout atualizado

- Piso 100 modulos: quatro tiles decorados no centro, piso comum no miolo, pedras e grades fechadas no perimetro. Nao sao adicionadas armadilhas ou buracos.
- Oito colunas, quatro barris, quatro conjuntos de caixas e quatro conjuntos de entulho nas laterais. A faixa central |X| < 14 continua livre dessas decoracoes.
- Dois arcos decorativos ao fundo, junto a parede. Seus colliders sao caixas solidas: nao sao novas passagens atravessaveis.
- Paredes dianteiras continuam baixas para reduzir oclusao; luz direcional em Realtime com sombras suaves e ajuste de bias.
- Novos modelos reais usados, na mesma pasta fbx(unity): floor_tile_big_grate, floor_tile_small_decorated, rubble_half, crates_stacked e wall_arched. Nada e reimportado ou editado nos originais.

### Verificacao em Play Mode

Depois de reconstruir, abra somente ArenaTeste. Entre em Play Mode, aguarde um segundo e execute Tools > Underdog > Validate Arena (Play Mode).

- [Arena Camera] informa Camera.main, camera renderizadora, cena, display, depth, rotacao real, tamanho ortografico, Brain e Live Camera. A rotacao deve ser aproximadamente (50,0,0), size 8.5, Brain ativo e Live Camera_Principal. Se aparecer 90, o problema nao e so um hint do Inspector: copie o log e confira a camera Live no Brain, outras cenas abertas, camera selecionada no display e scripts/Timelines que escrevem a rotacao. O comando acusa divergencias, mas nao altera a cena ou a camera durante o jogo.
- [Arena Rig] informa prioridade, Follow, LookAt, Body e Aim reais. Gire o Mage com o mouse: a rotacao da camera deve permanecer igual. Teste a mira e sua zona morta nos quatro quadrantes.
- A cena salva da primeira arena possui m_NavMeshData=0 e nao tem NavMeshSurface; portanto nao ha cobertura de NavMesh confirmada. O validador avisa se nao existir mesh carregada. Depois de um bake manual, ele amostra uma grade a cada 4 metros, ignora pontos dentro dos obstaculos do Environment e verifica caminhos completos ate o centro no NavMesh padrao. E uma amostragem de cobertura/conectividade, nao uma garantia de toda a superficie nem teste do controlador EnemyAI.
- O comando confere os colliders dos modulos Floor/Walls ativos. Nao simula dash, acertos de projeteis ou perseguicao. Esses testes continuam manuais.

### Roteiro de jogabilidade

1. Atravesse o centro, circule as laterais e use dash perpendicular e diagonal contra paredes/colunas/caixas, inclusive nos cantos. Nao deve atravessar nem cair pelas emendas.
2. Fique no miolo e atraia o esqueleto; teste tambem o percurso paralelo as bordas. EnemyAI ainda usa perseguicao direta: assar NavMesh nao lhe da pathfinding. Caso fique preso atras de decoracao, registre o ponto; nao adicione NavMeshAgent ativo em paralelo ao CharacterController.
3. Atire em caixas, entulho, colunas e paredes; o SphereCast do Projectile deve consumir o projetil no primeiro obstaculo. Atire no esqueleto e confirme vida, dano e morte. Mantenha o evento LancarMagia existente.
4. Ande e use dash enquanto ataca; verifique o tiro unico e a direcao compartilhada com o cajado.
5. Confira personagens e projeteis visiveis nos quatro cantos, em 16:9 e 4:3. Ortho 8.5 nao pretende mostrar todo o mapa. Pode haver oclusao localizada junto a um prop: valide antes de aumentar paredes ou colunas.
6. Para sombras, confira o asset URP e o shadow distance; o gerador nao altera o pipeline global. Para NavMesh, siga a configuracao manual da secao anterior e execute o validador novamente apos o bake.

A compilacao externa da segunda iteracao passou com Unity 2022.3 e Cinemachine 2.10. Permanecem os avisos de NavigationStatic obsoleto e ItemEffect.tipoEfeito sem atribuicao em codigo. Nao foram executados Editor/Play Mode, bake, capturas ou avaliacao visual nesta iteracao. Logs e checagens so fornecem resultados reais quando executados no Editor.
