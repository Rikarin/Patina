from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.project_page import ProjectPage
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    *,
    search: str | Unset = UNSET,
    tag: list[str] | Unset = UNSET,
    offset: int | Unset = 0,
    limit: int | Unset = 50,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    params["search"] = search

    json_tag: list[str] | Unset = UNSET
    if not isinstance(tag, Unset):
        json_tag = tag


    params["tag"] = json_tag

    params["offset"] = offset

    params["limit"] = limit


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/library/projects",
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> ProblemDetails | ProjectPage | None:
    if response.status_code == 200:
        response_200 = ProjectPage.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[ProblemDetails | ProjectPage]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    search: str | Unset = UNSET,
    tag: list[str] | Unset = UNSET,
    offset: int | Unset = 0,
    limit: int | Unset = 50,

) -> Response[ProblemDetails | ProjectPage]:
    """ One page of projects: by name, or by relevance when searching.

    Args:
        search (str | Unset):
        tag (list[str] | Unset):
        offset (int | Unset):  Default: 0.
        limit (int | Unset):  Default: 50.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ProjectPage]
     """


    kwargs = _get_kwargs(
        search=search,
tag=tag,
offset=offset,
limit=limit,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    search: str | Unset = UNSET,
    tag: list[str] | Unset = UNSET,
    offset: int | Unset = 0,
    limit: int | Unset = 50,

) -> ProblemDetails | ProjectPage | None:
    """ One page of projects: by name, or by relevance when searching.

    Args:
        search (str | Unset):
        tag (list[str] | Unset):
        offset (int | Unset):  Default: 0.
        limit (int | Unset):  Default: 50.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ProjectPage
     """


    return sync_detailed(
        client=client,
search=search,
tag=tag,
offset=offset,
limit=limit,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    search: str | Unset = UNSET,
    tag: list[str] | Unset = UNSET,
    offset: int | Unset = 0,
    limit: int | Unset = 50,

) -> Response[ProblemDetails | ProjectPage]:
    """ One page of projects: by name, or by relevance when searching.

    Args:
        search (str | Unset):
        tag (list[str] | Unset):
        offset (int | Unset):  Default: 0.
        limit (int | Unset):  Default: 50.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | ProjectPage]
     """


    kwargs = _get_kwargs(
        search=search,
tag=tag,
offset=offset,
limit=limit,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    search: str | Unset = UNSET,
    tag: list[str] | Unset = UNSET,
    offset: int | Unset = 0,
    limit: int | Unset = 50,

) -> ProblemDetails | ProjectPage | None:
    """ One page of projects: by name, or by relevance when searching.

    Args:
        search (str | Unset):
        tag (list[str] | Unset):
        offset (int | Unset):  Default: 0.
        limit (int | Unset):  Default: 50.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | ProjectPage
     """


    return (await asyncio_detailed(
        client=client,
search=search,
tag=tag,
offset=offset,
limit=limit,

    )).parsed
